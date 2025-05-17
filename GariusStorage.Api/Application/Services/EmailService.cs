using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http; // Adicionado
using System.Net.Http.Headers; // Adicionado
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using GariusStorage.Api.Application.Exceptions; // Importar exceções customizadas
using Microsoft.Extensions.Logging; // Adicionado

namespace GariusStorage.Api.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly ResendSettings _resendSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EmailService> _logger;

        private class ResendPayload
        {
            public string From { get; set; }
            public List<string> To { get; set; }
            public string Subject { get; set; }
            public string? Html { get; set; }
            public string? Text { get; set; }
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
                throw new InvalidOperationException("A API Key do Resend não pode ser nula ou vazia e deve ser configurada.");
            }
            if (string.IsNullOrWhiteSpace(_resendSettings.FromEmail))
            {
                _logger.LogError("O e-mail remetente (FromEmail) do Resend não está configurado.");
                throw new InvalidOperationException("O e-mail remetente (FromEmail) do Resend não pode ser nulo ou vazio e deve ser configurado.");
            }
        }

        // Alterado para retornar Task (void assíncrono)
        public async Task SendEmailAsync(string toEmail, string subject, string htmlContent, string? textContent = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Tentativa de enviar e-mail para um destinatário vazio.");
                throw new ValidationException("O endereço de e-mail do destinatário é obrigatório.", "EMAIL_RECIPIENT_EMPTY");
            }
            if (string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogWarning("Tentativa de enviar e-mail com assunto vazio.");
                throw new ValidationException("O assunto do e-mail é obrigatório.", "EMAIL_SUBJECT_EMPTY");
            }
            if (string.IsNullOrWhiteSpace(htmlContent) && string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("Tentativa de enviar e-mail sem conteúdo (HTML ou texto).");
                throw new ValidationException("O conteúdo do e-mail (HTML ou texto) é obrigatório.", "EMAIL_CONTENT_EMPTY");
            }


            var httpClient = _httpClientFactory.CreateClient("ResendApiClient");

            var payload = new ResendPayload
            {
                From = _resendSettings.FromEmail,
                To = new List<string> { toEmail },
                Subject = subject,
                Html = htmlContent,
                Text = textContent
            };

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            HttpResponseMessage response;
            try
            {
                // O BaseAddress do HttpClient já é "https://api.resend.com/"
                // A URL para enviar emails é "emails", então o PostAsJsonAsync usará "emails" como o requestUri relativo.
                response = await httpClient.PostAsJsonAsync("emails", payload, serializerOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Exceção de HttpRequest ao enviar e-mail para {ToEmail} via Resend.", toEmail);
                throw new OperationFailedException($"Falha na comunicação com o serviço de e-mail ao tentar enviar para {toEmail}. Tente novamente mais tarde.", "EMAIL_SERVICE_UNAVAILABLE", null, ex);
            }
            catch (JsonException ex) // Captura erros de serialização do payload ANTES do envio
            {
                _logger.LogError(ex, "Exceção de JSON ao preparar o payload para enviar e-mail para {ToEmail} via Resend.", toEmail);
                throw new OperationFailedException($"Erro interno ao preparar e-mail para {toEmail}.", "EMAIL_PAYLOAD_SERIALIZATION_ERROR", null, ex);
            }
            // Não precisamos de um catch genérico (Exception ex) aqui, pois o ErrorHandlingMiddleware cuidará disso.

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = "N/A";
                try
                {
                    errorContent = await response.Content.ReadAsStringAsync();
                }
                catch (Exception readEx)
                {
                    _logger.LogError(readEx, "Falha ao ler o conteúdo do erro da resposta do Resend para {ToEmail}.", toEmail);
                }

                _logger.LogError("Falha ao enviar e-mail para {ToEmail} via Resend. Status: {StatusCode}. Resposta: {ErrorContent}",
                    toEmail, response.StatusCode, errorContent);

                // Poderíamos tentar desserializar o errorContent para uma estrutura de erro do Resend se conhecida
                // e popular o 'details' da OperationFailedException.
                throw new OperationFailedException($"Falha ao enviar e-mail para {toEmail} (Serviço de E-mail retornou {(int)response.StatusCode}). Detalhes: {errorContent}", "EMAIL_SEND_FAILED_API_ERROR");
            }

            _logger.LogInformation("E-mail enviado com sucesso para {ToEmail} com assunto '{Subject}'.", toEmail, subject);
            // Se chegou aqui, o e-mail foi enviado com sucesso. O método completa.
        }

        public async Task SendEmailConfirmationLinkAsync(string userEmail, string userName, string confirmationLink)
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

            // SendEmailAsync agora lança exceção em caso de falha.
            await SendEmailAsync(userEmail, subject, htmlContent, textContent);
        }

        public async Task SendPasswordResetLinkAsync(string userEmail, string userName, string resetLink)
        {
            var subject = "Redefinição de Senha Solicitada";
            var htmlContent = $@"
                <h1>Olá {userName ?? "Usuário"},</h1>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta.</p>
                <p>Se você solicitou isso, clique no link abaixo para criar uma nova senha:</p>
                <p><a href='{resetLink}'>Redefinir Senha</a></p>
                <p>Este link de redefinição de senha expirará em um determinado período.</p>
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

            // SendEmailAsync agora lança exceção em caso de falha.
            await SendEmailAsync(userEmail, subject, htmlContent, textContent);
        }
    }
}
