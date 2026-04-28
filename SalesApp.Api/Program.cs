using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesApp.Data;
using SalesApp.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using AWS.Logger.SeriLog;
using AWS.Logger;
using Amazon;
using Microsoft.Extensions.Configuration;

namespace SalesApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var cwEnabled = Environment.GetEnvironmentVariable("CW_ERROR_LOG")?.ToLower() == "true";

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            if (cwEnabled)
            {
                var region = Environment.GetEnvironmentVariable("AWS__Region") ?? "us-east-1";
                var group = Environment.GetEnvironmentVariable("CW_LOG_GROUP") ?? 
                            Environment.GetEnvironmentVariable("AWS__CloudWatchLogGroup") ?? 
                            "/salesapp/api/errors";

                var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
                var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

                var awsConfigDict = new Dictionary<string, string?>
                {
                    { "Serilog:Region", region },
                    { "Serilog:LogGroup", group },
                    { "Serilog:AccessKey", accessKey },
                    { "Serilog:SecretKey", secretKey }
                };
                
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(awsConfigDict)
                    .Build();

                loggerConfig.WriteTo.AWSSeriLog(
                    configuration, 
                    restrictedToMinimumLevel: LogEventLevel.Error
                );
            }

            Log.Logger = loggerConfig.CreateLogger();
            
            Console.WriteLine("DEBUG: ENTERING MAIN...");
            try
            {
                Log.Information("Starting SalesApp...");
                var host = CreateHostBuilder(args).Build();
                
                // Seed database
                using (var scope = host.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    var context = services.GetRequiredService<AppDbContext>();
                    var environment = services.GetRequiredService<IHostEnvironment>();

                    if (environment.IsEnvironment("E2E"))
                    {
                        Log.Warning("==========================================================");
                        Log.Warning("E2E ENVIRONMENT DETECTED: DATA WILL BE RESET BY SEEDER");
                        Log.Warning("==========================================================");
                        
                        // Safety Double Check: Ensure we are NOT in Production
                        if (environment.IsProduction())
                        {
                            Log.Fatal("CRITICAL ERROR: Attempted to run E2E reset in PRODUCTION environment. Aborting startup.");
                            return;
                        }

                        // ✅ Force delete the E2E database file to ensure a completely fresh start
                        await context.Database.EnsureDeletedAsync();
                        Log.Warning("E2E DATABASE DELETED FOR FRESH START.");
                    }

                    int retries = 5;
                    while (retries > 0)
                    {
                        try
                        {
                            await context.Database.MigrateAsync();
                            await DbSeeder.SeedAsync(context);
                            break;
                        }
                        catch (Exception ex) when (retries > 1)
                        {
                            Log.Warning(ex, $"Database migration/seeding failed. Retrying... ({retries} attempts left)");
                            retries--;
                            await Task.Delay(1000); // Wait 1 second before retry
                        }
                        catch (Exception ex)
                        {
                            Log.Fatal(ex, "Database migration/seeding failed after multiple attempts.");
                            throw;
                        }
                    }

                    // 🚀 Final Safety: Small delay for SQLite filesystem release
                    await Task.Delay(500);

                    // 🚀 Initialize RBAC Cache here, AFTER migrations and seeding are complete
                    var rbacCache = services.GetRequiredService<IRbacCache>();
                    var rolePerms = await context.Roles
                        .Include(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                        .ToListAsync();

                    var cacheData = rolePerms.ToDictionary(
                        r => r.Id,
                        r => r.RolePermissions
                            .Select(rp => rp.Permission?.Name)
                            .Where(name => name != null)
                            .Cast<string>()
                            .ToHashSet()
                    );
                    rbacCache.Initialize(cacheData);
                    Log.Information("RBAC Cache initialized successfully.");

                    // CloudWatch Retention Initialization
                    if (cwEnabled)
                    {
                        var config = services.GetRequiredService<IConfiguration>();
                        var region = config["AWS:Region"] ?? "us-east-1";
                        var group = Environment.GetEnvironmentVariable("CW_LOG_GROUP") ?? 
                                    config["AWS:CloudWatchLogGroup"] ?? 
                                    "/salesapp/api/errors";
                        
                        var accessKey = config["AWS:AccessKeyId"] ?? Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
                        var secretKey = config["AWS:SecretAccessKey"] ?? Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

                        await CloudWatchRetentionInitializer.EnsureCloudWatchRetentionAsync(group, region, accessKey, secretKey);
                    }
                }
                
                host.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL: SalesApp terminated unexpectedly: {ex}");
                Log.Fatal(ex, "SalesApp terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog() // Use Serilog as the logging provider
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
