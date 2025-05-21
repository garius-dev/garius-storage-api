using Asp.Versioning;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Application.Mappers;
using GariusStorage.Api.Application.Services;
using GariusStorage.Api.Configuration;
using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Domain.Interfaces;
using GariusStorage.Api.Domain.Interfaces.Repositories;
using GariusStorage.Api.Infrastructure.Data;
using GariusStorage.Api.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Net.Http.Headers;
using System.Text;

namespace GariusStorage.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabase(
            this IServiceCollection services,
            string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptionsAction: sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                }));
            return services;
        }

        public static IServiceCollection AddCustomIdentity(this IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Configurações de senha
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredUniqueChars = 1;

                // Configurações de Lockout
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // Configurações de usuário
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = true;

                // Configurações de SignIn
                options.SignIn.RequireConfirmedAccount = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            return services;
        }

        public static IServiceCollection AddCustomAuthentication(
            this IServiceCollection services,
            JwtSettings jwtSettings,
            AuthenticationSettings externalAuthenticationSettings,
            IWebHostEnvironment environment)
        {
            var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/api/v1/auth/login";
                options.AccessDeniedPath = "/api/v1/auth/access-denied";
            })
            .AddGoogle(options =>
            {
                options.ClientId = externalAuthenticationSettings.Google.ClientId;
                options.ClientSecret = externalAuthenticationSettings.Google.ClientSecret;
                options.SaveTokens = true;
                options.CallbackPath = "/signin-google"; // Deve corresponder ao URI de redirecionamento no Google Cloud Console
                options.Scope.Add("profile");
                options.Scope.Add("email");
            })
            .AddMicrosoftAccount(options =>
            {
                options.ClientId = externalAuthenticationSettings.Microsoft.ClientId;
                options.ClientSecret = externalAuthenticationSettings.Microsoft.ClientSecret;
                options.SaveTokens = true;
                options.CallbackPath = "/signin-microsoft"; // Deve corresponder ao URI de redirecionamento na Microsoft
            });

            return services;
        }

        public static IServiceCollection AddHttpClients(
            this IServiceCollection services,
            ResendSettings resendSettings)
        {
            services.AddHttpClient("ResendApiClient", client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/"); // URL base da API do Resend
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", resendSettings.ApiKey);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

            // Adiciona um HttpClient padrão para o TurnstileService
            // Não precisa de configuração especial aqui, pois o TurnstileService constrói a URL completa.
            services.AddHttpClient("CloudflareTurnstileClient");


            return services;
        }

        public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "GariusStorage.Api - V1",
                    Version = "v1",
                    Description = "API para o sistema Garius Storage.",
                    Contact = new OpenApiContact
                    {
                        Name = "Garius Tech",
                        Email = "contato@gariustech.com", // Exemplo
                        Url = new Uri("https://gariustech.com") // Exemplo
                    }
                });

                // Configuração de segurança para JWT no Swagger
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Autorização JWT usando o esquema Bearer. \r\n\r\n Digite 'Bearer' [espaço] e o seu token no campo abaixo.\r\n\r\nExemplo: \"Bearer 12345abcdef\"",
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

                // Para garantir que o Swagger UI funcione corretamente com o versionamento
                options.DocInclusionPredicate((docName, apiDesc) =>
                {
                    if (!apiDesc.TryGetMethodInfo(out var methodInfo)) return false;

                    var versions = apiDesc.ActionDescriptor?.EndpointMetadata
                        .OfType<ApiVersionAttribute>()
                        .SelectMany(attr => attr.Versions)
                        .Select(v => $"v{v.MajorVersion}") ?? Enumerable.Empty<string>();

                    return versions.Contains(docName);
                });
            });
            return services;
        }

        public static IServiceCollection AddApiVersioningServices(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV"; // Formato para o nome do grupo no Swagger
                options.SubstituteApiVersionInUrl = true;
            });
            return services;
        }

        public static IServiceCollection AddCustomCors(this IServiceCollection services, IWebHostEnvironment environment)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", policyBuilder =>
                {
                    // Em desenvolvimento, pode ser mais flexível. Em produção, seja mais restritivo.
                    if (environment.IsDevelopment() || environment.IsEnvironment("LocalDevelopmentWithNgrok"))
                    {
                        policyBuilder.WithOrigins(
                                        "http://localhost:5173", // Frontend React local
                                        "https://localhost:5173",
                                        "https://jackal-infinite-penguin.ngrok-free.app" // Sua URL do ngrok para teste
                                       )
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    }
                    else
                    {
                        // Configure suas origens de produção aqui
                        // Exemplo: policyBuilder.WithOrigins("https://meufrontend.com")
                        //                      .WithMethods("GET", "POST", "PUT", "DELETE") // Métodos específicos
                        //                      .WithHeaders("Content-Type", "Authorization"); // Headers específicos
                        policyBuilder.AllowAnyOrigin() // Temporário para produção - RESTRINJA ISSO!
                                     .AllowAnyMethod()
                                     .AllowAnyHeader();
                    }
                });
            });
            return services;
        }

        // Método de extensão para aplicar a política de CORS no pipeline
        public static IApplicationBuilder UseCustomCors(this IApplicationBuilder app)
        {
            app.UseCors("AllowSpecificOrigins");
            return app;
        }


        public static IServiceCollection AddAutoMapperConfiguration(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(MappingProfile).Assembly);
            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<ITurnstileService, TurnstileService>();
            //---
            services.AddScoped<ICashFlowRepository, CashFlowRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<ICurrencyRepository, CurrencyRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IPurchaseItemRepository, PurchaseItemRepository>();
            services.AddScoped<IPurchaseRepository, PurchaseRepository>();
            services.AddScoped<ISaleItemRepository, SaleItemRepository>();
            services.AddScoped<ISaleRepository, SaleRepository>();
            services.AddScoped<ISellerRepository, SellerRepository>();
            services.AddScoped<IStockMovementRepository, StockMovementRepository>();
            services.AddScoped<IStockRepository, StockRepository>();
            services.AddScoped<IStorageLocationRepository, StorageLocationRepository>();
            services.AddScoped<ISupplierRepository, SupplierRepository>();

            return services;
        }

        public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options => AuthorizationPolicies.ConfigurePolicies(options));
            return services;
        }

        public static IServiceCollection AddForwardedHeadersOptions(this IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Limpar KnownProxies e KnownNetworks pode ser necessário se o proxy não estiver na rede local
                // options.KnownProxies.Clear();
                // options.KnownNetworks.Clear();
            });
            return services;
        }
    }
}
