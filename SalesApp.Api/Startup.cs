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

            
            // Database (SQLite)
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

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
            services.AddScoped<IUserMatriculaRepository, UserMatriculaRepository>();
            
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
            else
            {
                app.UseHttpsRedirection();
            }

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
