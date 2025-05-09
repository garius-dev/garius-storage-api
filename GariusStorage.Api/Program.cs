using Serilog.Events;
using Serilog;
using Gcp.SecretManager.Provider;
using GariusStorage.Api.Configuration;
using GariusStorage.Api.Helpers;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GariusStorage.Api.Application.Mappers;
using GariusStorage.Api.Extensions;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Asp.Versioning;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using GariusStorage.Api.Infrastructure.Middleware;
using GariusStorage.Api.Domain.Interfaces;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Application.Services;

// --- CONFIGURAÇÃO DO SERILOG --- //
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    //.WriteTo.Seq("http://localhost:5341")
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);


#region Services_Configuration

// --- CONFIGURAÇÃO DO GOOGLE SECRETS --- //
var googleCloudProjectId = builder.Configuration["GoogleCloud:ProjectId"];

if (!string.IsNullOrEmpty(googleCloudProjectId))
{
    builder.Configuration.AddGcpSecretManager(
        options =>
        {
            options.ProjectId = googleCloudProjectId;
        });
    Log.Information("Provedor de configuração do Google Cloud Secret Manager adicionado para o projeto: {ProjectId}", googleCloudProjectId);
}
else
{
    Log.Warning("Configuração 'GoogleCloud:ProjectId' não encontrada. O provedor do Secret Manager não será adicionado.");
}



// --- CONFIGURAÇÃO DOS OPTION BINDERS --- //
JwtSettings jwtSettings = new JwtSettings();

CloudflareSettings cloudflareSettings = new CloudflareSettings();
AuthenticationSettings externalAuthenticationSettings = new AuthenticationSettings();
CloudinarySettings cloudinarySettings = new CloudinarySettings();
ResendSettings resendSettings = new ResendSettings();

string currentConnectionString = string.Empty;

try
{
    var connectionStringSettings = LoadConfigHelper.LoadConfigFromSecret<Dictionary<string, string>>(builder.Configuration, "GariusStorageApi--ConnectionStrings");
    currentConnectionString = connectionStringSettings[builder.Environment.EnvironmentName];
    builder.Services.AddSingleton(Options.Create(currentConnectionString));
    

    cloudflareSettings = LoadConfigHelper.LoadConfigFromSecret<CloudflareSettings>(builder.Configuration, "GariusStorageApi--CloudflareSettings");
    builder.Services.AddSingleton(Options.Create(cloudflareSettings));

    externalAuthenticationSettings = LoadConfigHelper.LoadConfigFromSecret<AuthenticationSettings>(builder.Configuration, "GariusStorageApi--ExternalAuthenticationSettings");
    builder.Services.AddSingleton(Options.Create(externalAuthenticationSettings));

    cloudinarySettings = LoadConfigHelper.LoadConfigFromSecret<CloudinarySettings>(builder.Configuration, "GariusStorageApi--CloudinarySettings");
    builder.Services.AddSingleton(Options.Create(cloudinarySettings));

    resendSettings = LoadConfigHelper.LoadConfigFromSecret<ResendSettings>(builder.Configuration, "GariusStorageApi--ResendSettings");
    builder.Services.AddSingleton(Options.Create(resendSettings));

    jwtSettings = LoadConfigHelper.LoadConfigFromSecret<JwtSettings>(builder.Configuration, "MetalFlowScheduler-JwtSettings");
    builder.Services.AddSingleton(Options.Create(jwtSettings));

}
catch (InvalidOperationException ex)
{
    Log.Fatal(ex, "Falha grave na configuração dos secrets da aplicação.");
    Environment.Exit(1);
}
catch (JsonException ex)
{
    Log.Fatal(ex, "Falha ao desserializar a configuração JSON dos secrets.");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ocorreu um erro inesperado ao carregar a configuração.");
    Environment.Exit(1);
}



// --- CONFIGURAÇÃO DO BANCO DE DADOS --- //
builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseNpgsql(currentConnectionString,
    npgsqlOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// --- CONFIGURAÇÃO DO IDENTITY --- //
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Configurações de senha (ajuste conforme sua política de segurança)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 1;

    // Configurações de Lockout (bloqueio de conta)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Configurações de usuário
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true; // Requer email único

    // Configurações de SignIn
    options.SignIn.RequireConfirmedAccount = false; // Defina como true se usar confirmação de email
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;

})
.AddEntityFrameworkStores<ApplicationDbContext>() // Configura o Identity para usar EF Core com seu DbContext
.AddDefaultTokenProviders(); // Adiciona provedores de token para reset de senha, etc.



// --- CONFIGURAÇÃO DA AUTENTICAÇÃO VIA JWT --- //
var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, // Valida a assinatura do token usando a chave secreta
        IssuerSigningKey = new SymmetricSecurityKey(key), // A chave secreta usada para validar
        ValidateIssuer = true, // Valida se o emissor do token é o esperado
        ValidIssuer = jwtSettings.Issuer, // O emissor esperado (definido em appsettings ou user-secrets)
        ValidateAudience = true, // Valida se o público do token é o esperado
        ValidAudience = jwtSettings.Audience, // O público esperado (definido em appsettings ou user-secrets)
        ValidateLifetime = true, // Valida se o token não expirou
        ClockSkew = TimeSpan.Zero // Define a tolerância de tempo para expiração (zero é mais rigoroso)
    };
});

// --- CONFIGURAÇÃO DA AUTENTICAÇÃO VIA EXTERNAL LOGIN --- //
builder.Services.AddAuthentication()
    .AddCookie()
    .AddGoogle(options =>
    {
        
        options.ClientId = externalAuthenticationSettings.Google.ClientId;
        options.ClientSecret = externalAuthenticationSettings.Google.ClientSecret;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-google";
        options.Scope.Add("https://www.googleapis.com/auth/userinfo.profile");
        options.Scope.Add("https://www.googleapis.com/auth/userinfo.email");
    })
    .AddMicrosoftAccount(options =>
    {
        
        options.ClientId = externalAuthenticationSettings.Microsoft.ClientId;
        options.ClientSecret = externalAuthenticationSettings.Microsoft.ClientSecret;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-microsoft";
    });

// --- CONFIGURAÇÃO DO SWAGGER --- //
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configuração para a versão 1
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GariusStorage.Api - V1",
        Version = "v1"
    });

    // Configuração para o Swagger UI entender e permitir o envio do token JWT (Bearer)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization", // Nome do cabeçalho HTTP
        Type = SecuritySchemeType.ApiKey, // Tipo de esquema (ApiKey é usado para cabeçalhos)
        Scheme = "Bearer", // O esquema de autenticação (Bearer)
        BearerFormat = "JWT", // Formato do token (JWT)
        In = ParameterLocation.Header, // Onde o token será enviado (no cabeçalho)
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer ' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
    });

    // Adiciona a exigência de segurança (o token Bearer) para todos os endpoints no Swagger UI
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer" // Deve corresponder ao nome definido em AddSecurityDefinition
                }
            },
            new string[] {} // Escopos necessários (vazio para JWT simples)
        }
    });

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

// --- CONFIGURAÇÃO DO VERSIONAMENTO DO SWAGGER --- //
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader(); // Lê a versão de um segmento da URL, ex.: /api/v2
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV"; // Formato do grupo (ex.: v2)
    options.SubstituteApiVersionInUrl = true;
});



// --- CONFIGURAÇÃO DO CORS --- //
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", builder =>
    {
        builder.WithOrigins("http://localhost:5173", "https://localhost:5173", "https://jackal-infinite-penguin.ngrok-free.app")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});



// --- CONFIGURAÇÃO DO AUTO-MAPPER --- //
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);


// --- CONFIGURAÇÃO DO SERILOG --- //
builder.Host.UseSerilog();


// --- CONFIGURAÇÃO DOS CONTROLLERS --- //
builder.Services.AddControllers();


// --- CONFIGURAÇÃO DA AUTORIZAÇÃO COM POLICES --- //
builder.Services.AddAuthorization(options => AuthorizationPolicies.ConfigurePolicies(options));



#endregion

// --- INJEÇÃO DE DEPENDÊNCIAS -- //
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

// --- CONFIGURAÇÃO DO MIDDLEWARE DE EXCEPTIONS --- //
app.UseErrorHandlingMiddleware();

// --- CONFIGURAÇÃO DO SERILOG --- //
app.UseSerilogRequestLogging();

// --- CONFIGURAÇÃO DO SWAGGER --- //
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GariusStorage API V1");
        options.RoutePrefix = "swagger";
        options.DefaultModelExpandDepth(-1); // Opcional: oculta o schema de modelos
    });

}

// --- CONFIGURAÇÃO DO CORS --- //
app.UseCors("AllowReactApp");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Iniciando a aplicação MetalFlowScheduler API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush();
}