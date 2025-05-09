using System.Text.Json.Serialization;

namespace GariusStorage.Api.Configuration
{
    public class ResendSettings
    {
        [JsonPropertyName("ApiKey")]
        public string ApiKey { get; set; }

        [JsonPropertyName("FromEmail")]
        public string FromEmail { get; set; }
    }
}
