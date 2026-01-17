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
using System.IdentityModel.Tokens.Jwt;
using System.Text;

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
            services.AddScoped<IGroupRepository, GroupRepository>();
            services.AddScoped<IContractRepository, ContractRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPVRepository, PVRepository>();
            services.AddScoped<IUserMatriculaRepository, UserMatriculaRepository>();

            services.AddScoped<IUserHierarchyService, UserHierarchyService>();
            services.AddScoped<IDynamicRoleAuthorizationService, DynamicRoleAuthorizationService>();
            services.AddScoped<IEndpointDiscoveryService, EndpointDiscoveryService>();
            
            // Import repositories
            services.AddScoped<IImportTemplateRepository, ImportTemplateRepository>();
            services.AddScoped<IImportSessionRepository, ImportSessionRepository>();
            services.AddScoped<IImportColumnMappingRepository, ImportColumnMappingRepository>();
            services.AddScoped<IImportUserMappingRepository, ImportUserMappingRepository>();
            
            // Import services
            services.AddScoped<IFileParserService, FileParserService>();
            services.AddScoped<IAutoMappingService, AutoMappingService>();
            services.AddScoped<IUserMatchingService, UserMatchingService>();
            services.AddScoped<IImportValidationService, ImportValidationService>();
            services.AddScoped<IImportExecutionService, ImportExecutionService>();
            services.AddScoped<IContractAggregationService, ContractAggregationService>();
            
            // Message service for translations
            services.AddScoped<IMessageService, MessageService>();
            
            // Email services
            services.AddScoped<IEmailSender, SesEmailSender>();
            services.AddScoped<IEmailService, EmailService>();
            
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