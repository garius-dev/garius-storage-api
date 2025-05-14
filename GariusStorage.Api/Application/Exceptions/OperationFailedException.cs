namespace GariusStorage.Api.Application.Exceptions
{
    public class OperationFailedException : BaseApplicationException
    {
        public OperationFailedException(string message, string? errorCode = null, Dictionary<string, string[]>? details = null, Exception? innerException = null)
            : base(message, errorCode, details, innerException)
        {
        }
    }
}