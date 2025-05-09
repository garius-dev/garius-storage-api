namespace GariusStorage.Api.Application.Dtos
{
    public class ExternalLoginDto
    {
        public string Provider { get; set; } = string.Empty; // "Google"
        public string IdToken { get; set; } = string.Empty;  // Token recebido do frontend
    }
}
