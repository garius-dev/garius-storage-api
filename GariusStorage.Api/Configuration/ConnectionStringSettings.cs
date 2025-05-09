using System.Text.Json.Serialization;

namespace GariusStorage.Api.Configuration
{
    public class ConnectionStringSettings
    {
        [JsonPropertyName("Development")]
        public ConnectionStringDetail Development { get; set; }

        [JsonPropertyName("Google")]
        public ConnectionStringDetail Production { get; set; }
    }

    public class ConnectionStringDetail
    {
        [JsonPropertyName("Value")]
        public string Value { get; set; }
    }
}
