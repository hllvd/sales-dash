using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesApp.Data;
using Serilog;
using System.IO;
using Microsoft.EntityFrameworkCore;

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
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    // Ensure database directory exists for SQLite
                    var connectionString = context.Database.GetDbConnection().ConnectionString;
                    if (connectionString.Contains("Data Source="))
                    {
                        var dataSource = connectionString.Split("Data Source=")[1].Split(";")[0];
                        var directory = Path.GetDirectoryName(dataSource);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Log.Information("Creating database directory: {Directory}", directory);
                            Directory.CreateDirectory(directory);
                        }
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
