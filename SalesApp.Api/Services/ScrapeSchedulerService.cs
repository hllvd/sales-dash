using Microsoft.EntityFrameworkCore;
using SalesApp.Data;

namespace SalesApp.Services
{
    public class ScrapeSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScrapeSchedulerService> _logger;
        private readonly IConfiguration _configuration;

        public ScrapeSchedulerService(
            IServiceProvider serviceProvider,
            ILogger<ScrapeSchedulerService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var isEnabled = _configuration.GetValue<bool>("Scheduler:Enabled", true);
            if (!isEnabled)
            {
                _logger.LogInformation("ScrapeSchedulerService está desativado via configuração.");
                return;
            }

            _logger.LogInformation("ScrapeSchedulerService iniciado. Monitorando contas com intervalo agendado a cada 60s.");

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

            var now = DateTime.UtcNow;

            var configsToProcess = await dbContext.ScrapeConfigs
                .Include(c => c.User)
                .Where(c => c.IsEnabled
                    && c.ScrapeIntervalHours != null
                    && c.ScrapeIntervalHours >= 1
                    && c.CredentialStatus != "wrong-password")
                .ToListAsync(cancellationToken);

            foreach (var config in configsToProcess)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool shouldTrigger = false;

                if (config.LastTriggeredAt == null)
                {
                    shouldTrigger = true;
                }
                else
                {
                    var elapsed = now - config.LastTriggeredAt.Value;
                    if (elapsed.TotalHours >= config.ScrapeIntervalHours.Value)
                    {
                        shouldTrigger = true;
                    }
                }

                if (shouldTrigger)
                {
                    _logger.LogInformation(
                        "Scrape agendado vencido para conta {Id} (Matrícula: {Matricula}, Intervalo: {Hours}h). Disparando...",
                        config.Id, config.Matricula, config.ScrapeIntervalHours);

                    try
                    {
                        var runId = $"cron-{DateTime.UtcNow:yyyyMMddHHmmss}-{config.Matricula}";
                        
                        // Executa o disparo através do Orchestrator
                        await orchestrator.TriggerScrapeAsync(
                            configId: config.Id,
                            isManual: false,
                            runId: runId,
                            userEmail: config.User?.Email ?? "scheduler@system",
                            scrapeDate: null, // ScrapeOrchestrator/Worker calculará com base no DefaultStartMonth relativo
                            scrapeType: config.ScrapeType,
                            outputMode: config.OutputMode
                        );

                        config.LastTriggeredAt = now;
                        config.UpdatedAt = now;
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
    }
}
