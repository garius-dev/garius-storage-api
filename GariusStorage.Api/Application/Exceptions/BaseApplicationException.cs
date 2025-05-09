namespace GariusStorage.Api.Application.Exceptions
{
    public abstract class BaseApplicationException : Exception
    {
        public string? ErrorCode { get; set; }

        public Dictionary<string, string[]>? Details { get; set; }

        protected BaseApplicationException(string message, string? errorCode = null, Dictionary<string, string[]>? details = null, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Details = details;
        }
    }
}
