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

// --- CONFIGURA��O DO SERILOG --- //
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

// --- CONFIGURA��O DO GOOGLE SECRETS --- //
var googleCloudProjectId = builder.Configuration["GoogleCloud:ProjectId"];

if (!string.IsNullOrEmpty(googleCloudProjectId))
{
    builder.Configuration.AddGcpSecretManager(
        options =>
        {
            options.ProjectId = googleCloudProjectId;
        });
    Log.Information("Provedor de configura��o do Google Cloud Secret Manager adicionado para o projeto: {ProjectId}", googleCloudProjectId);
}
else
{
    Log.Warning("Configura��o 'GoogleCloud:ProjectId' n�o encontrada. O provedor do Secret Manager n�o ser� adicionado.");
}



// --- CONFIGURA��O DOS OPTION BINDERS --- //
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
    Log.Fatal(ex, "Falha grave na configura��o dos secrets da aplica��o.");
    Environment.Exit(1);
}
catch (JsonException ex)
{
    Log.Fatal(ex, "Falha ao desserializar a configura��o JSON dos secrets.");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ocorreu um erro inesperado ao carregar a configura��o.");
    Environment.Exit(1);
}



// --- CONFIGURA��O DO BANCO DE DADOS --- //
builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseNpgsql(currentConnectionString,
    npgsqlOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// --- CONFIGURA��O DO IDENTITY --- //
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Configura��es de senha (ajuste conforme sua pol�tica de seguran�a)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 1;

    // Configura��es de Lockout (bloqueio de conta)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Configura��es de usu�rio
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true; // Requer email �nico

    // Configura��es de SignIn
    options.SignIn.RequireConfirmedAccount = false; // Defina como true se usar confirma��o de email
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;

})
.AddEntityFrameworkStores<ApplicationDbContext>() // Configura o Identity para usar EF Core com seu DbContext
.AddDefaultTokenProviders(); // Adiciona provedores de token para reset de senha, etc.



// --- CONFIGURA��O DA AUTENTICA��O VIA JWT --- //
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
        ValidateIssuer = true, // Valida se o emissor do token � o esperado
        ValidIssuer = jwtSettings.Issuer, // O emissor esperado (definido em appsettings ou user-secrets)
        ValidateAudience = true, // Valida se o p�blico do token � o esperado
        ValidAudience = jwtSettings.Audience, // O p�blico esperado (definido em appsettings ou user-secrets)
        ValidateLifetime = true, // Valida se o token n�o expirou
        ClockSkew = TimeSpan.Zero // Define a toler�ncia de tempo para expira��o (zero � mais rigoroso)
    };
});

// --- CONFIGURA��O DA AUTENTICA��O VIA EXTERNAL LOGIN --- //
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

// --- CONFIGURA��O DO SWAGGER --- //
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configura��o para a vers�o 1
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GariusStorage.Api - V1",
        Version = "v1"
    });

    // Configura��o para o Swagger UI entender e permitir o envio do token JWT (Bearer)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization", // Nome do cabe�alho HTTP
        Type = SecuritySchemeType.ApiKey, // Tipo de esquema (ApiKey � usado para cabe�alhos)
        Scheme = "Bearer", // O esquema de autentica��o (Bearer)
        BearerFormat = "JWT", // Formato do token (JWT)
        In = ParameterLocation.Header, // Onde o token ser� enviado (no cabe�alho)
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer ' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
    });

    // Adiciona a exig�ncia de seguran�a (o token Bearer) para todos os endpoints no Swagger UI
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
            new string[] {} // Escopos necess�rios (vazio para JWT simples)
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

// --- CONFIGURA��O DO VERSIONAMENTO DO SWAGGER --- //
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader(); // L� a vers�o de um segmento da URL, ex.: /api/v2
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV"; // Formato do grupo (ex.: v2)
    options.SubstituteApiVersionInUrl = true;
});



// --- CONFIGURA��O DO CORS --- //
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", builder =>
    {
        builder.WithOrigins("http://localhost:5173", "https://localhost:5173", "https://jackal-infinite-penguin.ngrok-free.app")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});



// --- CONFIGURA��O DO AUTO-MAPPER --- //
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);


// --- CONFIGURA��O DO SERILOG --- //
builder.Host.UseSerilog();


// --- CONFIGURA��O DOS CONTROLLERS --- //
builder.Services.AddControllers();


// --- CONFIGURA��O DA AUTORIZA��O COM POLICES --- //
builder.Services.AddAuthorization(options => AuthorizationPolicies.ConfigurePolicies(options));



#endregion

// --- INJE��O DE DEPEND�NCIAS -- //
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

// --- CONFIGURA��O DO MIDDLEWARE DE EXCEPTIONS --- //
app.UseErrorHandlingMiddleware();

// --- CONFIGURA��O DO SERILOG --- //
app.UseSerilogRequestLogging();

// --- CONFIGURA��O DO SWAGGER --- //
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

// --- CONFIGURA��O DO CORS --- //
app.UseCors("AllowReactApp");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Iniciando a aplica��o MetalFlowScheduler API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplica��o falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush();
}