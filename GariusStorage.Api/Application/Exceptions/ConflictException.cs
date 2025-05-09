namespace GariusStorage.Api.Application.Exceptions
{
    public class ConflictException : BaseApplicationException
    {
        public ConflictException(string message, string? errorCode = null, Exception? innerException = null)
            : base(message, errorCode, null, innerException)
        {
        }
    }
}
