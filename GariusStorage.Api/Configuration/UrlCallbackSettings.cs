namespace GariusStorage.Api.Configuration
{
    public class UrlCallbackSettings
    {
        public string Environment { get; set; }
        public Dictionary<string,string> UrlCallbacks { get; set; }

        public UrlCallbackSettings(string environment, Dictionary<string, string> urlCallbacks)
        {
            Environment = environment;
            UrlCallbacks = urlCallbacks;
        }
    }
}
