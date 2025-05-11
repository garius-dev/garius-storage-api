using System.Text.Json.Serialization;

namespace GariusStorage.Api.Application.Dtos
{
    public class TurnstileVerificationResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; set; } // ISO_date_time

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("error-codes")]
        public List<string>? ErrorCodes { get; set; } = new List<string>();

        [JsonPropertyName("action")]
        public string? Action { get; set; } // Se você usou 'action' no widget

        [JsonPropertyName("cdata")]
        public string? CustomData { get; set; } // Se você usou 'cdata' no widget
    }
}
