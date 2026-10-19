using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services
{
    public class WeeklySchedule
    {
        public int DayOfWeek { get; set; } = 1; // 0 = Domingo, 1 = Segunda, ... 6 = Sábado
        public List<string> Times { get; set; } = new();
    }

    public class ScrapeSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScrapeSchedulerService> _logger;
        private readonly IConfiguration _configuration;
        private static readonly TimeZoneInfo BrasiliaTimeZone = GetBrasiliaTimeZone();

        public ScrapeSchedulerService(
            IServiceProvider serviceProvider,
            ILogger<ScrapeSchedulerService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        private static TimeZoneInfo GetBrasiliaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasilia Standard Time", "Brasilia Standard Time");
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var isEnabled = _configuration.GetValue<bool>("Scheduler:Enabled", true);
            if (!isEnabled)
            {
                _logger.LogInformation("ScrapeSchedulerService está desativado via configuração.");
                return;
            }

            _logger.LogInformation("ScrapeSchedulerService iniciado. Monitorando contas agendadas (Fuso: América/São Paulo) a cada 60s.");

            // Delay inicial de 15 segundos para estabilização do container
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndTriggerScheduledScrapesAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro no loop do ScrapeSchedulerService.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("ScrapeSchedulerService encerrando.");
        }

        private async Task CheckAndTriggerScheduledScrapesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IScrapeOrchestrator>();

            var nowUtc = DateTime.UtcNow;

            var configsToProcess = await dbContext.ScrapeConfigs
                .Include(c => c.User)
                .Where(c => c.IsEnabled
                    && c.CredentialStatus != "wrong-password"
                    && (
                        (c.ScheduleMode == "interval" && ((c.ScheduleIntervalHours != null && c.ScheduleIntervalHours >= 1) || (c.ScrapeIntervalHours != null && c.ScrapeIntervalHours >= 1)))
                        || (c.ScheduleMode == "daily" && !string.IsNullOrEmpty(c.ScheduleTimes))
                        || (c.ScheduleMode == "weekly" && !string.IsNullOrEmpty(c.ScheduleTimes))
                        || (c.ScheduleMode == null && c.ScrapeIntervalHours != null && c.ScrapeIntervalHours >= 1)
                    ))
                .ToListAsync(cancellationToken);

            foreach (var config in configsToProcess)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var (shouldTrigger, reason) = EvaluateSchedule(config, nowUtc);

                if (shouldTrigger)
                {
                    _logger.LogInformation(
                        "Scrape agendado ({Reason}) para conta {Id} (Matrícula: {Matricula}, Modo: {Mode}). Disparando...",
                        reason, config.Id, config.Matricula, config.ScheduleMode ?? "interval");

                    try
                    {
                        var runId = $"cron-{DateTime.UtcNow:yyyyMMddHHmmss}-{config.Matricula}";
                        
                        await orchestrator.TriggerScrapeAsync(
                            configId: config.Id,
                            isManual: false,
                            runId: runId,
                            userEmail: config.User?.Email ?? "scheduler@system",
                            scrapeDate: null,
                            scrapeType: config.ScrapeType,
                            outputMode: config.OutputMode
                        );

                        config.LastTriggeredAt = nowUtc;
                        config.UpdatedAt = nowUtc;
                        await dbContext.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation("Scrape agendado disparado com sucesso para matrícula {Matricula} (RunId: {RunId})", config.Matricula, runId);
                    }
                    catch (Exception triggerEx)
                    {
                        _logger.LogError(triggerEx, "Falha ao disparar scrape agendado para matrícula {Matricula}", config.Matricula);
                    }
                }
            }
        }

        public static (bool ShouldTrigger, string Reason) EvaluateSchedule(ScrapeConfig config, DateTime nowUtc)
        {
            var mode = config.ScheduleMode;

            // Fallback para contas legadas sem ScheduleMode definido mas com ScrapeIntervalHours
            if (string.IsNullOrEmpty(mode) && config.ScrapeIntervalHours.HasValue && config.ScrapeIntervalHours.Value >= 1)
            {
                mode = "interval";
            }

            if (mode == "interval")
            {
                var intervalHours = config.ScheduleIntervalHours ?? config.ScrapeIntervalHours ?? 24;
                if (intervalHours < 1) return (false, string.Empty);

                if (config.LastTriggeredAt == null)
                {
                    return (true, $"Primeiro disparo do intervalo ({intervalHours}h)");
                }

                var elapsed = nowUtc - config.LastTriggeredAt.Value;
                if (elapsed.TotalHours >= intervalHours)
                {
                    return (true, $"Intervalo vencido ({elapsed.TotalHours:F1}h >= {intervalHours}h)");
                }

                return (false, string.Empty);
            }

            if (mode == "daily")
            {
                List<string>? times = null;
                try
                {
                    times = JsonSerializer.Deserialize<List<string>>(config.ScheduleTimes ?? "[]");
                }
                catch
                {
                    return (false, string.Empty);
                }

                if (times == null || times.Count == 0) return (false, string.Empty);

                var nowBrasilia = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, BrasiliaTimeZone);
                var todayBrasilia = nowBrasilia.Date;

                foreach (var timeStr in times)
                {
                    if (!TimeSpan.TryParse(timeStr, out var timeOfDay)) continue;

                    var scheduledBrasilia = todayBrasilia.Add(timeOfDay);
                    var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(scheduledBrasilia, BrasiliaTimeZone);

                    if (nowUtc >= scheduledUtc)
                    {
                        var elapsedSinceSlot = nowUtc - scheduledUtc;
                        // Janela de tolerância de 15 minutos
                        if (elapsedSinceSlot.TotalMinutes <= 15)
                        {
                            if (config.LastTriggeredAt == null || config.LastTriggeredAt.Value < scheduledUtc.AddMinutes(-1))
                            {
                                return (true, $"Horário diário atingido ({timeStr} Brasília)");
                            }
                        }
                    }
                }

                return (false, string.Empty);
            }

            if (mode == "weekly")
            {
                WeeklySchedule? weekly = null;
                try
                {
                    weekly = JsonSerializer.Deserialize<WeeklySchedule>(config.ScheduleTimes ?? "{}");
                }
                catch
                {
                    return (false, string.Empty);
                }

                if (weekly == null || weekly.Times == null || weekly.Times.Count == 0) return (false, string.Empty);

                var nowBrasilia = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, BrasiliaTimeZone);
                if ((int)nowBrasilia.DayOfWeek != weekly.DayOfWeek) return (false, string.Empty);

                var todayBrasilia = nowBrasilia.Date;
                foreach (var timeStr in weekly.Times)
                {
                    if (!TimeSpan.TryParse(timeStr, out var timeOfDay)) continue;

                    var scheduledBrasilia = todayBrasilia.Add(timeOfDay);
                    var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(scheduledBrasilia, BrasiliaTimeZone);

                    if (nowUtc >= scheduledUtc)
                    {
                        var elapsedSinceSlot = nowUtc - scheduledUtc;
                        if (elapsedSinceSlot.TotalMinutes <= 15)
                        {
                            if (config.LastTriggeredAt == null || config.LastTriggeredAt.Value < scheduledUtc.AddMinutes(-1))
                            {
                                return (true, $"Horário semanal atingido (Dia {weekly.DayOfWeek}, {timeStr} Brasília)");
                            }
                        }
                    }
                }

                return (false, string.Empty);
            }

            return (false, string.Empty);
        }
    }
}
