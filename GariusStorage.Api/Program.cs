using Serilog;
using Serilog.Events;
using Gcp.SecretManager.Provider;
using GariusStorage.Api.Configuration;
using GariusStorage.Api.Helpers;
using Microsoft.Extensions.Options;
using GariusStorage.Api.Extensions; // Local dos métodos de extensão
using GariusStorage.Api.Infrastructure.Middleware;
using Newtonsoft.Json; // Necessário para o catch de JsonException

// --- CONFIGURAÇÃO INICIAL DO SERILOG ---
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
    Log.Information("Iniciando a configuração da aplicação GariusStorage API...");

    var builder = WebApplication.CreateBuilder(args);

    // --- CONFIGURAÇÃO DO GOOGLE SECRETS ---
    var googleCloudProjectId = builder.Configuration["GoogleCloud:ProjectId"];
    if (!string.IsNullOrEmpty(googleCloudProjectId))
    {
        builder.Configuration.AddGcpSecretManager(options => options.ProjectId = googleCloudProjectId);
        Log.Information("Provedor de configuração do Google Cloud Secret Manager adicionado para o projeto: {ProjectId}", googleCloudProjectId);
    }
    else
    {
        Log.Warning("Configuração 'GoogleCloud:ProjectId' não encontrada. O provedor do Secret Manager não será adicionado.");
    }

    // --- CARREGAMENTO E CONFIGURAÇÃO DE SECRETS E OPÇÕES ---
    // É importante carregar as configurações que serão usadas por outros serviços ANTES de registrá-los.
    var jwtSettings = new JwtSettings();
    var cloudflareSettings = new CloudflareSettings();
    var externalAuthenticationSettings = new AuthenticationSettings();
    var cloudinarySettings = new CloudinarySettings(); // Embora não usado diretamente no Program.cs, é carregado aqui.
    var resendSettings = new ResendSettings();
    string currentConnectionString = string.Empty;
    UrlCallbackSettings urlCallbackSettings;

    try
    {
        var connectionStringSettingsDict = LoadConfigHelper.LoadConfigFromSecret<Dictionary<string, string>>(builder.Configuration, "GariusStorageApi--ConnectionStrings");
        currentConnectionString = connectionStringSettingsDict[builder.Environment.EnvironmentName];
        builder.Services.AddSingleton(Options.Create(currentConnectionString)); // Para uso futuro, se necessário

        cloudflareSettings = LoadConfigHelper.LoadConfigFromSecret<CloudflareSettings>(builder.Configuration, "GariusStorageApi--CloudflareSettings");
        externalAuthenticationSettings = LoadConfigHelper.LoadConfigFromSecret<AuthenticationSettings>(builder.Configuration, "GariusStorageApi--ExternalAuthenticationSettings");
        cloudinarySettings = LoadConfigHelper.LoadConfigFromSecret<CloudinarySettings>(builder.Configuration, "GariusStorageApi--CloudinarySettings");
        resendSettings = LoadConfigHelper.LoadConfigFromSecret<ResendSettings>(builder.Configuration, "GariusStorageApi--ResendSettings");

        var urlCallbacksResponse = LoadConfigHelper.LoadConfigFromSecret<Dictionary<string, string>>(builder.Configuration, "GariusStorageApi--UrlCallbackSettings");
        urlCallbackSettings = new UrlCallbackSettings(builder.Environment.EnvironmentName, urlCallbacksResponse);

        jwtSettings = LoadConfigHelper.LoadConfigFromSecret<JwtSettings>(builder.Configuration, "GariusStorageApi--JwtSettings");

        // Disponibiliza as configurações carregadas como IOptions<T> para injeção de dependência
        builder.Services.AddSingleton(Options.Create(jwtSettings));
        builder.Services.AddSingleton(Options.Create(cloudflareSettings));
        builder.Services.AddSingleton(Options.Create(externalAuthenticationSettings));
        builder.Services.AddSingleton(Options.Create(cloudinarySettings));
        builder.Services.AddSingleton(Options.Create(resendSettings));
        builder.Services.AddSingleton(Options.Create(urlCallbackSettings));
    }
    catch (InvalidOperationException ex)
    {
        Log.Fatal(ex, "Falha grave na configuração dos secrets da aplicação.");
        Environment.Exit(1); // Encerra a aplicação se secrets críticos não puderem ser carregados
    }
    catch (JsonException ex)
    {
        Log.Fatal(ex, "Falha ao desserializar a configuração JSON dos secrets.");
        Environment.Exit(1);
    }
    catch (Exception ex) // Captura genérica para outros erros inesperados no carregamento de config
    {
        Log.Fatal(ex, "Ocorreu um erro inesperado ao carregar a configuração inicial.");
        Environment.Exit(1);
    }

    // --- REGISTRO DOS SERVIÇOS DA APLICAÇÃO USANDO MÉTODOS DE EXTENSÃO ---
    builder.Services.AddDatabase(currentConnectionString);
    builder.Services.AddCustomIdentity();
    builder.Services.AddCustomAuthentication(jwtSettings, externalAuthenticationSettings, builder.Environment);
    builder.Services.AddHttpClients(resendSettings); // Configura HttpClient para Resend
    builder.Services.AddSwaggerServices();
    builder.Services.AddApiVersioningServices();
    builder.Services.AddCustomCors(builder.Environment); // Passa o environment para políticas de CORS dinâmicas se necessário
    builder.Services.AddAutoMapperConfiguration();
    builder.Services.AddApplicationServices();
    builder.Services.AddCustomAuthorization();
    builder.Services.AddForwardedHeadersOptions();

    builder.Services.AddControllers();
    builder.Host.UseSerilog(); // Configura Serilog para ser o provedor de logging da aplicação

    // --- CONSTRUÇÃO DA APLICAÇÃO ---
    var app = builder.Build();

    // --- CONFIGURAÇÃO DO PIPELINE HTTP ---
    app.UseForwardedHeaders(); // Deve ser um dos primeiros middlewares
    app.UseErrorHandlingMiddleware(); // Middleware de tratamento de erros customizado
    app.UseSerilogRequestLogging(); // Loga todas as requisições HTTP

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("LocalDevelopmentWithNgrok"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "GariusStorage API V1");
            options.RoutePrefix = "swagger"; // Acessar Swagger UI em /swagger
            options.DefaultModelExpandDepth(-1); // Oculta os schemas por padrão
        });
    }

    app.UseRouting();
    app.UseCustomCors(); // Aplica a política de CORS configurada
    app.UseHttpsRedirection();
    app.UseAuthentication(); // Middleware de autenticação
    app.UseAuthorization(); // Middleware de autorização

    app.MapControllers();

    Log.Information("Aplicação GariusStorage API configurada e pronta para iniciar.");
    app.Run();
}
catch (Exception ex) // Captura exceções durante a inicialização da aplicação (fora do pipeline HTTP)
{
    Log.Fatal(ex, "Aplicação GariusStorage API falhou ao iniciar.");
}
finally
{
    Log.CloseAndFlush(); // Garante que todos os logs sejam escritos antes de fechar
}
