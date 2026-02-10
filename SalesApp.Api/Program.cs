using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesApp.Data;
using SalesApp.Services;
using Microsoft.EntityFrameworkCore;
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
                        Log.Warning("E2E ENVIRONMENT DETECTED: DATA WILL BE RESET BY SEEDER");
                        Log.Warning("==========================================================");
                        
                        // Safety Double Check: Ensure we are NOT in Production
                        if (environment.IsProduction())
                        {
                            Log.Fatal("CRITICAL ERROR: Attempted to run E2E reset in PRODUCTION environment. Aborting startup.");
                            return;
                        }
                    }

                    int retries = 5;
                    while (retries > 0)
                    {
                        try
                        {
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
