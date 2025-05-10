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
using Microsoft.AspNetCore.HttpOverrides;
using System.Net.Http.Headers; // Adicionar este using

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

    var resendUrlCallbackSettings = LoadConfigHelper.LoadConfigFromSecret<Dictionary<string, string>>(builder.Configuration, "GariusStorageApi--ResendUrlCallbackSettings");
    var _confirmEmailUrl = resendUrlCallbackSettings[$"{builder.Environment.EnvironmentName}--ConfirmEmailUrl"];
    var _resetPasswordUrl = resendUrlCallbackSettings[$"{builder.Environment.EnvironmentName}--ResetPasswordUrl"];
    var resendUrlCallbackSettingsObj = new ResendUrlCallbackSettings(_confirmEmailUrl, _resetPasswordUrl);
    builder.Services.AddSingleton(Options.Create(resendUrlCallbackSettingsObj));

    jwtSettings = LoadConfigHelper.LoadConfigFromSecret<JwtSettings>(builder.Configuration, "MetalFlowScheduler-JwtSettings"); // Atenção: Nome do secret parece ser de outro projeto. Verifique se é o correto.
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
    options.SignIn.RequireConfirmedAccount = true; // Alterado para true para exigir confirmação de email para usuários locais
    options.SignIn.RequireConfirmedEmail = true; // Garante que o email confirmado seja necessário
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
    // Para login externo, o Scheme padrão pode ser o do Cookie se você quiser que o SignInManager lide com o cookie de correlação.
    // options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme; // Adicionar se usar cookies para login externo
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment(); // Em desenvolvimento pode ser false
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
// --- CONFIGURAÇÃO DA AUTENTICAÇÃO VIA EXTERNAL LOGIN (Google, Microsoft, etc.) --- //
// Adicionar autenticação por Cookie é crucial para que o SignInManager funcione corretamente com logins externos
// Ele usa um cookie temporário (correlação) para rastrear o estado entre a chamada para o provedor externo e o callback.
.AddCookie(options => // Configuração do Cookie para logins externos e Identity
{
    options.LoginPath = "/api/v1/auth/login"; // Ou uma página de login se você tiver uma UI na API
    options.AccessDeniedPath = "/api/v1/auth/access-denied"; // Endpoint para acesso negado
    // options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // Opcional: tempo de vida do cookie de autenticação
    // options.SlidingExpiration = true; // Opcional
})
.AddGoogle(options =>
{
    options.ClientId = externalAuthenticationSettings.Google.ClientId;
    options.ClientSecret = externalAuthenticationSettings.Google.ClientSecret;
    options.SaveTokens = true; // Salva tokens do Google (access_token, refresh_token) se necessário
    // O CallbackPath DEVE corresponder ao URI de redirecionamento configurado no Google Cloud Console
    // E também ao endpoint que lida com o callback na sua API (ExternalLoginCallback no AuthController)
    options.CallbackPath = "/signin-google"; // Ajuste se o seu endpoint de callback for diferente
    options.Scope.Add("profile"); // Adiciona o escopo "profile"
    options.Scope.Add("email");   // Adiciona o escopo "email"
})
.AddMicrosoftAccount(options =>
{
    options.ClientId = externalAuthenticationSettings.Microsoft.ClientId;
    options.ClientSecret = externalAuthenticationSettings.Microsoft.ClientSecret;
    options.SaveTokens = true;
    options.CallbackPath = "/signin-microsoft"; // Ajuste para o seu endpoint de callback da Microsoft
});

// --- CONFIGURAÇÃO DO HTTPCLIENTFACTORY PARA RESEND --- //
builder.Services.AddHttpClient("ResendApiClient", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/emails"); // URL base da API do Resend
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", resendSettings.ApiKey); // Usa a ApiKey carregada
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// --- CONFIGURAÇÃO DO SWAGGER --- //
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GariusStorage.Api - V1",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer ' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
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
            new string[] {}
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
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});



// --- CONFIGURAÇÃO DO CORS --- //
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policyBuilder => // Renomeado para maior clareza
    {
        policyBuilder.WithOrigins(
                        "http://localhost:5173", // Frontend React local
                        "https://localhost:5173",
                        "https://jackal-infinite-penguin.ngrok-free.app" // Sua URL do ngrok
                       )
               .AllowAnyMethod()
               .AllowAnyHeader();
        // Se o Swagger UI for acessado por uma URL diferente (ex: a da API via ngrok)
        // e precisar de CORS (o que não deveria ser o caso para o redirecionamento 302 inicial),
        // você adicionaria essa origem aqui também.
        // Ex: .WithOrigins("https://sua-api-url-ngrok.com")
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

// --- CONFIGURAÇÃO PARA FORWARDED HEADERS (PROXY REVERSO COMO NGROK) --- //
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Se o ngrok ou outro proxy não estiver rodando no localhost em relação à sua app,
    // você pode precisar limpar KnownProxies e KnownNetworks.
    // options.KnownProxies.Clear();
    // options.KnownNetworks.Clear();
});

#endregion

// --- INJEÇÃO DE DEPENDÊNCIAS -- //
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>(); // Registrar AuthService
                                                         // Adicionar IEmailService e IUserManagementService quando criados
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();


var app = builder.Build();

// --- CONFIGURAÇÃO DO MIDDLEWARE DE FORWARDED HEADERS --- //
// Deve ser um dos primeiros middlewares, especialmente antes de UseAuthentication e outros que dependem do scheme/host correto.
app.UseForwardedHeaders();

// --- CONFIGURAÇÃO DO MIDDLEWARE DE EXCEPTIONS --- //
app.UseErrorHandlingMiddleware();

// --- CONFIGURAÇÃO DO SERILOG --- //
app.UseSerilogRequestLogging();

// --- CONFIGURAÇÃO DO SWAGGER --- //
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("LocalDevelopmentWithNgrok")) // Permite Swagger com ngrok também
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GariusStorage API V1");
        options.RoutePrefix = "swagger";
        options.DefaultModelExpandDepth(-1);
    });
}

// --- CONFIGURAÇÃO DO CORS --- //
app.UseCors("AllowSpecificOrigins"); // Usar a política nomeada

app.UseHttpsRedirection();

// A ordem é importante: Authentication antes de Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Iniciando a aplicação GariusStorage API..."); // Nome da aplicação atualizado
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
