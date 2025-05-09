using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GariusStorage.Api.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly ResendSettings _resendSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EmailService> _logger;

        // Estrutura para o payload da API do Resend
        private class ResendPayload
        {
            public string From { get; set; }
            public List<string> To { get; set; } // ALTERADO: De List<EmailRecipient> para List<string>
            public string Subject { get; set; }
            public string? Html { get; set; }
            public string? Text { get; set; }
            // Outros campos como Bcc, Cc, ReplyTo, Tags podem ser adicionados aqui
        }


        public EmailService(
            IOptions<ResendSettings> resendSettingsOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<EmailService> logger)
        {
            _resendSettings = resendSettingsOptions.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_resendSettings.ApiKey))
            {
                _logger.LogError("A API Key do Resend não está configurada.");
                throw new InvalidOperationException("A API Key do Resend não pode ser nula ou vazia.");
            }
            if (string.IsNullOrWhiteSpace(_resendSettings.FromEmail))
            {
                _logger.LogError("O e-mail remetente (FromEmail) do Resend não está configurado.");
                throw new InvalidOperationException("O e-mail remetente (FromEmail) do Resend não pode ser nulo ou vazio.");
            }
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent, string? textContent = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Tentativa de enviar e-mail para um destinatário vazio.");
                return false;
            }

            var httpClient = _httpClientFactory.CreateClient("ResendApiClient");

            var payload = new ResendPayload
            {
                From = _resendSettings.FromEmail,
                To = new List<string> { toEmail }, // ALTERADO: Instanciação direta da lista de strings
                Subject = subject,
                Html = htmlContent,
                Text = textContent
            };

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            try
            {
                // Log do payload antes de enviar para depuração (CUIDADO: pode conter dados sensíveis como e-mails)
                // string jsonPayloadForLogging = JsonSerializer.Serialize(payload, serializerOptions);
                // _logger.LogDebug("Payload do Resend a ser enviado: {JsonPayload}", jsonPayloadForLogging);

                var response = await httpClient.PostAsJsonAsync("emails", payload, serializerOptions);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("E-mail enviado com sucesso para {ToEmail} com assunto '{Subject}'.", toEmail, subject);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Falha ao enviar e-mail para {ToEmail} via Resend. Status: {StatusCode}. Resposta: {ErrorContent}",
                        toEmail, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Exceção de HttpRequest ao enviar e-mail para {ToEmail} via Resend.", toEmail);
                return false;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Exceção de JSON ao preparar o payload para enviar e-mail para {ToEmail} via Resend.", toEmail);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção inesperada ao enviar e-mail para {ToEmail} via Resend.", toEmail);
                return false;
            }
        }

        public async Task<bool> SendEmailConfirmationLinkAsync(string userEmail, string userName, string confirmationLink)
        {
            var subject = "Confirme seu endereço de e-mail";
            var htmlContent = $@"
                <h1>Olá {userName ?? "Usuário"},</h1>
                <p>Obrigado por se registrar! Por favor, confirme seu endereço de e-mail clicando no link abaixo:</p>
                <p><a href='{confirmationLink}'>Confirmar E-mail</a></p>
                <p>Se você não se registrou, por favor ignore este e-mail.</p>
                <br>
                <p>Atenciosamente,</p>
                <p>Equipe GariusStorage</p>";

            var textContent = $@"
                Olá {userName ?? "Usuário"},
                Obrigado por se registrar! Por favor, confirme seu endereço de e-mail copiando e colando o seguinte link no seu navegador:
                {confirmationLink}
                Se você não se registrou, por favor ignore este e-mail.
                Atenciosamente,
                Equipe GariusStorage";

            return await SendEmailAsync(userEmail, subject, htmlContent, textContent);
        }

        public async Task<bool> SendPasswordResetLinkAsync(string userEmail, string userName, string resetLink)
        {
            var subject = "Redefinição de Senha Solicitada";
            var htmlContent = $@"
                <h1>Olá {userName ?? "Usuário"},</h1>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta.</p>
                <p>Se você solicitou isso, clique no link abaixo para criar uma nova senha:</p>
                <p><a href='{resetLink}'>Redefinir Senha</a></p>
                <p>Este link de redefinição de senha expirará em um determinado período (geralmente 1 hora, dependendo da configuração do token do Identity).</p>
                <p>Se você não solicitou uma redefinição de senha, nenhuma ação é necessária e sua senha permanecerá a mesma.</p>
                <br>
                <p>Atenciosamente,</p>
                <p>Equipe GariusStorage</p>";

            var textContent = $@"
                Olá {userName ?? "Usuário"},
                Recebemos uma solicitação para redefinir a senha da sua conta.
                Se você solicitou isso, copie e cole o seguinte link no seu navegador para criar uma nova senha:
                {resetLink}
                Este link de redefinição de senha expirará em um determinado período.
                Se você não solicitou uma redefinição de senha, nenhuma ação é necessária.
                Atenciosamente,
                Equipe GariusStorage";

            return await SendEmailAsync(userEmail, subject, htmlContent, textContent);
        }
    }
}
