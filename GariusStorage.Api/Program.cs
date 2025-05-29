using Serilog;
using Serilog.Events;
using Gcp.SecretManager.Provider;
using GariusStorage.Api.Configuration;
using GariusStorage.Api.Helpers;
using Microsoft.Extensions.Options;
using GariusStorage.Api.Extensions; // Local dos m�todos de extens�o
using GariusStorage.Api.Infrastructure.Middleware;
using Newtonsoft.Json; // Necess�rio para o catch de JsonException

// --- CONFIGURA��O INICIAL DO SERILOG ---
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    //.WriteTo.Seq("http://localhost:5341") // Descomente se for usar Seq
    .CreateLogger();

try
{
    Log.Information("Iniciando a configura��o da aplica��o GariusStorage API...");

    var builder = WebApplication.CreateBuilder(args);

    // --- CONFIGURA��O DO GOOGLE SECRETS ---
    var googleCloudProjectId = builder.Configuration["GoogleCloud:ProjectId"];
    if (!string.IsNullOrEmpty(googleCloudProjectId))
    {
        builder.Configuration.AddGcpSecretManager(options => options.ProjectId = googleCloudProjectId);
        Log.Information("Provedor de configura��o do Google Cloud Secret Manager adicionado para o projeto: {ProjectId}", googleCloudProjectId);
    }
    else
    {
        Log.Warning("Configura��o 'GoogleCloud:ProjectId' n�o encontrada. O provedor do Secret Manager n�o ser� adicionado.");
    }

    // --- CARREGAMENTO E CONFIGURA��O DE SECRETS E OP��ES ---
    // � importante carregar as configura��es que ser�o usadas por outros servi�os ANTES de registr�-los.
    var jwtSettings = new JwtSettings();
    var cloudflareSettings = new CloudflareSettings();
    var externalAuthenticationSettings = new AuthenticationSettings();
    var cloudinarySettings = new CloudinarySettings(); // Embora n�o usado diretamente no Program.cs, � carregado aqui.
    var resendSettings = new ResendSettings();
    string currentConnectionString = string.Empty;
    UrlCallbackSettings urlCallbackSettings;

    try
    {
        var connectionStringSettingsDict = LoadConfigHelper.LoadConfigFromSecret<Dictionary<string, string>>(builder.Configuration, "GariusStorageApi--ConnectionStrings");
        currentConnectionString = connectionStringSettingsDict[builder.Environment.EnvironmentName];
        builder.Services.AddSingleton(Options.Create(currentConnectionString)); // Para uso futuro, se necess�rio

        cloudflareSettings = LoadConfigHelper.LoadConfigFromSecret<CloudflareSettings>(builder.Configuration, "GariusStorageApi--CloudflareSettings");
        externalAuthenticationSettings = LoadConfigHelper.LoadConfigFromSecret<AuthenticationSettings>(builder.Configuration, "GariusStorageApi--ExternalAuthenticationSettings");
        cloudinarySettings = LoadConfigHelper.LoadConfigFromSecret<CloudinarySettings>(builder.Configuration, "GariusStorageApi--CloudinarySettings");
        resendSettings = LoadConfigHelper.LoadConfigFromSecret<ResendSettings>(builder.Configuration, "GariusStorageApi--ResendSettings");

        var urlCallbacksResponse = LoadConfigHelper.LoadConfigFromSecret<Dictionary<string, string>>(builder.Configuration, "GariusStorageApi--UrlCallbackSettings");
        urlCallbackSettings = new UrlCallbackSettings(builder.Environment.EnvironmentName, urlCallbacksResponse);

        jwtSettings = LoadConfigHelper.LoadConfigFromSecret<JwtSettings>(builder.Configuration, "GariusStorageApi--JwtSettings");

        // Disponibiliza as configura��es carregadas como IOptions<T> para inje��o de depend�ncia
        builder.Services.AddSingleton(Options.Create(jwtSettings));
        builder.Services.AddSingleton(Options.Create(cloudflareSettings));
        builder.Services.AddSingleton(Options.Create(externalAuthenticationSettings));
        builder.Services.AddSingleton(Options.Create(cloudinarySettings));
        builder.Services.AddSingleton(Options.Create(resendSettings));
        builder.Services.AddSingleton(Options.Create(urlCallbackSettings));
    }
    catch (InvalidOperationException ex)
    {
        Log.Fatal(ex, "Falha grave na configura��o dos secrets da aplica��o.");
        Environment.Exit(1); // Encerra a aplica��o se secrets cr�ticos n�o puderem ser carregados
    }
    catch (JsonException ex)
    {
        Log.Fatal(ex, "Falha ao desserializar a configura��o JSON dos secrets.");
        Environment.Exit(1);
    }
    catch (Exception ex) // Captura gen�rica para outros erros inesperados no carregamento de config
    {
        Log.Fatal(ex, "Ocorreu um erro inesperado ao carregar a configura��o inicial.");
        Environment.Exit(1);
    }

    // --- REGISTRO DOS SERVI�OS DA APLICA��O USANDO M�TODOS DE EXTENS�O ---
    builder.Services.AddDatabase(currentConnectionString);
    builder.Services.AddCustomIdentity();
    builder.Services.AddCustomAuthentication(jwtSettings, externalAuthenticationSettings, builder.Environment);
    builder.Services.AddHttpClients(resendSettings); // Configura HttpClient para Resend
    builder.Services.AddSwaggerServices();
    builder.Services.AddApiVersioningServices();
    builder.Services.AddCustomCors(builder.Environment); // Passa o environment para pol�ticas de CORS din�micas se necess�rio
    builder.Services.AddAutoMapperConfiguration();
    builder.Services.AddApplicationServices();
    builder.Services.AddCustomAuthorization();
    builder.Services.AddForwardedHeadersOptions();

    builder.Services.AddControllers();
    builder.Host.UseSerilog(); // Configura Serilog para ser o provedor de logging da aplica��o

    // --- CONSTRU��O DA APLICA��O ---
    var app = builder.Build();

    // --- CONFIGURA��O DO PIPELINE HTTP ---
    app.UseForwardedHeaders(); // Deve ser um dos primeiros middlewares
    app.UseErrorHandlingMiddleware(); // Middleware de tratamento de erros customizado
    app.UseSerilogRequestLogging(); // Loga todas as requisi��es HTTP

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("LocalDevelopmentWithNgrok"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "GariusStorage API V1");
            options.RoutePrefix = "swagger"; // Acessar Swagger UI em /swagger
            options.DefaultModelExpandDepth(-1); // Oculta os schemas por padr�o
        });
    }

    app.UseRouting();
    app.UseCustomCors(); // Aplica a pol�tica de CORS configurada
    app.UseHttpsRedirection();
    app.UseAuthentication(); // Middleware de autentica��o
    app.UseAuthorization(); // Middleware de autoriza��o

    app.MapControllers();

    Log.Information("Aplica��o GariusStorage API configurada e pronta para iniciar.");
    app.Run();
}
catch (Exception ex) // Captura exce��es durante a inicializa��o da aplica��o (fora do pipeline HTTP)
{
    Log.Fatal(ex, "Aplica��o GariusStorage API falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush(); // Garante que todos os logs sejam escritos antes de fechar
}
