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
using SalesApp.Repositories;
using SalesApp.Services;
using SalesApp.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace SalesApp
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHealthChecks();

            
            // Database (SQLite)
            services.AddDbContext<AppDbContext>(options =>
            {
                var connectionString = Configuration.GetConnectionString("DefaultConnection");
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "E2E")
                {
                    connectionString = connectionString? .Replace("SalesApp.db", "SalesApp.E2E.db");
                }
                options.UseSqlite(connectionString);
            });

            // SQLite Performance PRAGMAs (applied at startup)
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dbConnection = dbContext.Database.GetDbConnection();
                dbConnection.Open();
                using (var command = dbConnection.CreateCommand())
                {
                    command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA cache_size=-100000;";
                    command.ExecuteNonQuery();
                }
            }

            // Data Protection (fix encryption warning)
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
            services.AddScoped<IUserHierarchyService, UserHierarchyService>();
            
            // Production-Grade RBAC
            services.AddSingleton<IRbacCache, RbacCache>();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();
            
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
            services.AddScoped<IUserMatriculaRepository, UserMatriculaRepository>();
            services.AddScoped<IContractMetadataRepository, ContractMetadataRepository>();
            services.AddScoped<IWizardService, WizardService>();
            
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
                           .AllowAnyHeader()
                           .WithExposedHeaders("WWW-Authenticate"); // Expose authentication error header
                });
            });
            
            // Controllers with security configurations
            services.AddControllers(options =>
            {
                // Suppress automatic 400 responses to allow custom error handling
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = false;
            })
            .AddJsonOptions(options =>
            {
                // Security: Reject unknown JSON properties to prevent mass assignment/property pollution
                options.JsonSerializerOptions.UnmappedMemberHandling = 
                    System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
            });

            // JWT Authentication
            var jwtKey = Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
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
                    ClockSkew = TimeSpan.Zero,

                };
            });
            
            services.AddAuthorization();

            // Swagger
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "SalesApp API", Version = "v1" });
                
                // Bearer Token authentication
                c.AddSecurityDefinition("Bearer", new()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT Bearer token"
                });
                
                c.AddSecurityRequirement(new()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
                
                // Support for file uploads - must come before OperationFilter
                c.MapType<IFormFile>(() => new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                });
                
                // Custom operation filter for file uploads
                c.OperationFilter<FileUploadOperationFilter>();
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SalesApp API v1"));
            }
            // Removed app.UseHttpsRedirection() as it's handled by Nginx proxy

            app.UseRouting();
            
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });
        }
    }
}
