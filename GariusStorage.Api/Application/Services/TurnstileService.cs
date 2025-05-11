using GariusStorage.Api.Application.Dtos;
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
                return new TurnstileVerificationResponseDto { Success = false, ErrorCodes = new List<string> { "missing-input-response" } };
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

            try
            {
                // Cloudflare espera os parâmetros como form-urlencoded
                var requestContent = new FormUrlEncodedContent(parameters);

                var response = await httpClient.PostAsync(CloudflareSiteVerifyUrl, requestContent);

                if (response.IsSuccessStatusCode)
                {
                    var verificationResponse = await response.Content.ReadFromJsonAsync<TurnstileVerificationResponseDto>();
                    if (verificationResponse == null)
                    {
                        _logger.LogError("Falha ao desserializar a resposta de verificação do Turnstile. Resposta vazia.");
                        return new TurnstileVerificationResponseDto { Success = false, ErrorCodes = new List<string> { "deserialization-error" } };
                    }

                    if (!verificationResponse.Success)
                    {
                        _logger.LogWarning("Validação do Turnstile falhou. Token: {Token}. Erros: {ErrorCodes}",
                            token,
                            string.Join(", ", verificationResponse.ErrorCodes ?? new List<string>()));
                    }
                    else
                    {
                        _logger.LogInformation("Token do Turnstile validado com sucesso. Hostname: {Hostname}, Timestamp: {Timestamp}",
                            verificationResponse.Hostname, verificationResponse.ChallengeTimestamp);
                    }
                    return verificationResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro ao chamar a API de verificação do Turnstile. Status: {StatusCode}. Resposta: {ErrorContent}",
                        response.StatusCode, errorContent);
                    return new TurnstileVerificationResponseDto { Success = false, ErrorCodes = new List<string> { $"http-error-{(int)response.StatusCode}" } };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Exceção de HttpRequest ao validar token do Turnstile.");
                return new TurnstileVerificationResponseDto { Success = false, ErrorCodes = new List<string> { "http-request-exception" } };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Exceção de JSON ao desserializar resposta do Turnstile.");
                return new TurnstileVerificationResponseDto { Success = false, ErrorCodes = new List<string> { "json-exception" } };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção inesperada ao validar token do Turnstile.");
                return new TurnstileVerificationResponseDto { Success = false, ErrorCodes = new List<string> { "unexpected-exception" } };
            }
        }
    }
}
