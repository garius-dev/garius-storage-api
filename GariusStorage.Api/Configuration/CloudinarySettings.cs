using System.Text.Json.Serialization;

namespace GariusStorage.Api.Configuration
{
    public class CloudinarySettings
    {
        [JsonPropertyName("CloudName")]
        public string CloudName { get; set; }

        [JsonPropertyName("ApiKey")]
        public string ApiKey { get; set; }

        [JsonPropertyName("ApiSecret")]
        public string ApiSecret { get; set; }
    }
}
