using System.Text.Json.Serialization;

namespace GariusStorage.Api.Configuration
{
    public class AuthenticationSettings
    {
        [JsonPropertyName("Google")]
        public GoogleAuthSettings Google { get; set; }

        [JsonPropertyName("Microsoft")]
        public MicrosoftAuthSettings Microsoft { get; set; }
    }

    public class GoogleAuthSettings
    {
        [JsonPropertyName("ClientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("ClientSecret")]
        public string ClientSecret { get; set; }
    }

    public class MicrosoftAuthSettings
    {
        [JsonPropertyName("ClientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("ClientSecret")]
        public string ClientSecret { get; set; }

        [JsonPropertyName("TenantId")]
        public string TenantId { get; set; }
    }
}
