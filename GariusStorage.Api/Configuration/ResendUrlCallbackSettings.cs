namespace GariusStorage.Api.Configuration
{
    public class ResendUrlCallbackSettings
    {
        public string ConfirmEmailUrl { get; set; } = string.Empty;
        public string ResetPasswordUrl { get; set; } = string.Empty;

        public ResendUrlCallbackSettings(string confirmEmailUrl, string resetPasswordUrl)
        {
            ConfirmEmailUrl = confirmEmailUrl;
            ResetPasswordUrl = resetPasswordUrl;
        }
    }
}
