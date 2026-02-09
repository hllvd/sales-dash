using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesApp.Data;
using Serilog;

namespace SalesApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
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
                        Log.Warning("E2E ENVIRONMENT DETECTED: DELETING AND RECREATING DATABASE");
                        Log.Warning("==========================================================");
                        
                        // Safety Double Check: Ensure we are NOT in Production
                        if (environment.IsProduction())
                        {
                            Log.Fatal("CRITICAL ERROR: Attempted to run E2E reset in PRODUCTION environment. Aborting startup.");
                            return;
                        }

                        await context.Database.EnsureDeletedAsync();
                        Log.Information("E2E database deleted successfully.");
                    }

                    await DbSeeder.SeedAsync(context);
                }
                
                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "SalesApp terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
