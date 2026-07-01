using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Moq;
using Amazon.DynamoDBv2;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using SalesApp.Authorization;
using Microsoft.AspNetCore.Authorization;
using SalesApp.ReportFilters.Repositories;
using SalesApp.ReportFilters.Services;
using SalesApp.ReportFilters.Settings;

namespace SalesApp.IntegrationTests
{
    public class TestStartup
    {
        public IConfiguration Configuration { get; }

        public TestStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            // Database (SQLite for tests) - connection string provided by TestWebApplicationFactory
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            // Data Protection
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "keys")))
                .SetApplicationName("SalesApp");

            // Services
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<IGroupRepository, GroupRepository>();
            services.AddScoped<IContractRepository, ContractRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPVRepository, PVRepository>();
            services.AddScoped<IUserMatriculaRepository, UserMatriculaRepository>();
            services.AddScoped<IContractMetadataRepository, ContractMetadataRepository>();
            services.AddScoped<IUserMetadataRepository, UserMetadataRepository>();
            services.AddScoped<IMatriculaRepository, MatriculaRepository>();
            services.AddScoped<IPendingContractClaimRepository, PendingContractClaimRepository>();

            services.AddScoped<IUserHierarchyService, UserHierarchyService>();
            services.AddScoped<IUserScopeService, UserScopeService>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddScoped<IWizardService, WizardService>();
            services.AddScoped<IWizardHeaderValidator, WizardHeaderValidator>();
            services.AddScoped<IContractStatusService, ContractStatusService>();

            // Monitoring & Notifications
            services.AddScoped<IMonitoringRepository, MonitoringRepository>();
            services.AddScoped<IMonitoringService, MonitoringService>();
            services.AddSingleton<INotificationService, LoggingNotificationService>();
            
            // Classification feature repositories
            services.AddScoped<IClassificationLevelRepository, ClassificationLevelRepository>();
            services.AddScoped<IUserClassificationRepository, UserClassificationRepository>();
            
            // AWS DynamoDB Mock/Placeholder for activation
            services.AddSingleton<IAmazonDynamoDB>(new Mock<IAmazonDynamoDB>().Object);

            // Scraping Services
            services.AddScoped<IScrapeDynamoLogService, ScrapeDynamoLogService>();
            services.AddScoped<IImportErrorService, ImportErrorService>();
            services.AddScoped<IScrapeImportService, ScrapeImportService>();
            services.AddScoped<IScrapeOrchestrator, ScrapeOrchestrator>();

            // Typed HttpClient for pbi-scraper
            services.AddHttpClient<PbiScraperClient>(client =>
            {
                client.BaseAddress = new Uri(Configuration["PbiScraper:BaseUrl"] ?? "http://pbi-scraper:3001");
            });
            
            // Production-Grade RBAC
            services.AddSingleton<IRbacCache, RbacCache>();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();
            services.AddScoped<IUserScopeService, UserScopeService>();
            
            // Import repositories
            services.AddScoped<IImportTemplateRepository, ImportTemplateRepository>();
            services.AddScoped<IImportSessionRepository, ImportSessionRepository>();
            services.AddScoped<IImportColumnMappingRepository, ImportColumnMappingRepository>();
            
            // Import services
            services.AddScoped<IFileParserService, FileParserService>();
            services.AddScoped<IAutoMappingService, AutoMappingService>();
            services.AddScoped<IImportValidationService, ImportValidationService>();
            services.AddScoped<IImportExecutionService, ImportExecutionService>();
            services.AddScoped<IContractAggregationService, ContractAggregationService>();
            
            // Message service for translations
            services.AddScoped<IMessageService, MessageService>();
            
            // Email services
            services.AddScoped<IEmailSender, SesEmailSender>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IPendingClaimService, PendingClaimService>();

            // Contract Status Aliases Mapping
            services.Configure<Models.Configuration.ContractStatusOptions>(Configuration.GetSection("ContractStatusMappings"));
            services.AddSingleton<IContractStatusMapper, ContractStatusMapper>();
            
            // DynamoDb typed settings
            services.Configure<DynamoDbSettings>(Configuration.GetSection("AWS"));

            // Report Filters feature
            services.AddScoped<IReportFilterRepository, DynamoDbReportFilterRepository>();
            services.AddScoped<IReportFilterService, ReportFilterService>();
            
            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });
            
            // Controllers - scan from main API assembly
            services.AddControllers()
                .AddApplicationPart(typeof(SalesApp.Controllers.RolesController).Assembly);

            // JWT Configuration - matches main API Startup.cs
            var jwtKey = Configuration["Jwt:Key"] ?? "test-secret-key-for-integration-tests-that-is-long-enough";
            var key = Encoding.ASCII.GetBytes(jwtKey);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });
            
            services.AddAuthorization();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}