using GariusStorage.Api.Application.Dtos;
using GariusStorage.Api.Application.Exceptions;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GariusStorage.Api.Application.Services
{
    public class TurnstileService : ITurnstileService
    {
        private readonly CloudflareSettings _cloudflareSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TurnstileService> _logger;
        private const string CloudflareSiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        public TurnstileService(
            IOptions<CloudflareSettings> cloudflareSettingsOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<TurnstileService> logger)
        {
            _cloudflareSettings = cloudflareSettingsOptions.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_cloudflareSettings.SecretKey))
            {
                _logger.LogError("A Secret Key do Cloudflare Turnstile não está configurada.");
                throw new InvalidOperationException("A Secret Key do Cloudflare Turnstile não pode ser nula ou vazia.");
            }
        }

        public async Task<TurnstileVerificationResponseDto?> ValidateTokenAsync(string token, string? remoteIp = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Token do Turnstile fornecido para validação está vazio.");
                throw new ValidationException("O token de verificação (CAPTCHA) é obrigatório.", "CAPTCHA_TOKEN_MISSING",
                    new Dictionary<string, string[]> { { "turnstileToken", new[] { "O token do Turnstile é obrigatório." } } });
            }

            var httpClient = _httpClientFactory.CreateClient("CloudflareTurnstileClient");

            var parameters = new Dictionary<string, string>
            {
                { "secret", _cloudflareSettings.SecretKey },
                { "response", token }
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                parameters.Add("remoteip", remoteIp);
            }

            HttpResponseMessage response;
            try
            {
                var requestContent = new FormUrlEncodedContent(parameters);
                response = await httpClient.PostAsync(CloudflareSiteVerifyUrl, requestContent);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Exceção de HttpRequest ao validar token do Turnstile com Cloudflare.");
                throw new OperationFailedException("Falha na comunicação com o serviço de verificação CAPTCHA. Tente novamente mais tarde.", "CAPTCHA_SERVICE_UNAVAILABLE", null, ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro ao chamar a API de verificação do Turnstile. Status: {StatusCode}. Resposta: {ErrorContent}",
                    response.StatusCode, errorContent);
                throw new OperationFailedException($"Falha ao verificar o token CAPTCHA (HTTP {(int)response.StatusCode}).", "CAPTCHA_API_HTTP_ERROR");
            }

            TurnstileVerificationResponseDto? verificationResponse;
            try
            {
                verificationResponse = await response.Content.ReadFromJsonAsync<TurnstileVerificationResponseDto>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Falha ao desserializar a resposta de verificação do Turnstile.");
                throw new OperationFailedException("Falha ao processar a resposta do serviço de verificação CAPTCHA.", "CAPTCHA_RESPONSE_DESERIALIZATION_ERROR", null, ex);
            }

            if (verificationResponse == null)
            {
                _logger.LogError("Resposta de verificação do Turnstile desserializada para null.");
                throw new OperationFailedException("Resposta inválida do serviço de verificação CAPTCHA.", "CAPTCHA_NULL_RESPONSE");
            }

            if (!verificationResponse.Success)
            {
                _logger.LogWarning("Validação do Turnstile falhou. Token: {Token}. Erros: {ErrorCodes}",
                    token, // Não logar o token em produção se for sensível
                    string.Join(", ", verificationResponse.ErrorCodes ?? new List<string>()));

                // Mapear error-codes do Turnstile para ValidationException se apropriado
                var errorDetails = verificationResponse.ErrorCodes?.ToDictionary(ec => ec, ec => new[] { $"Turnstile error: {ec}" });
                throw new ValidationException("Falha na verificação de segurança (CAPTCHA).", "CAPTCHA_VERIFICATION_FAILED", errorDetails);
            }

            _logger.LogInformation("Token do Turnstile validado com sucesso. Hostname: {Hostname}, Timestamp: {Timestamp}",
                verificationResponse.Hostname, verificationResponse.ChallengeTimestamp);

            return verificationResponse;
        }
    }
}
