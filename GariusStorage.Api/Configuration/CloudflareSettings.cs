using System.Text.Json.Serialization;

namespace GariusStorage.Api.Configuration
{
    public class CloudflareSettings
    {
        [JsonPropertyName("SiteKey")]
        public string SiteKey { get; set; }

        [JsonPropertyName("SecretKey")]
        public string SecretKey { get; set; }
    }
}
