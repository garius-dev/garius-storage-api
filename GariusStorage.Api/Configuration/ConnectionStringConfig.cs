namespace GariusStorage.Api.Configuration
{
    public class ConnectionStringConfig
    {
        public string Environment { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }

    public class ConnectionStringsConfig
    {
        public List<ConnectionStringConfig> ConnectionStrings { get; set; } = new List<ConnectionStringConfig>();
    }
}
