namespace GariusStorage.Api.Application.Exceptions
{
    public class PermissionDeniedException : BaseApplicationException
    {
        public PermissionDeniedException(string message = "Você não tem permissão para realizar esta ação.", string? errorCode = null, Exception? innerException = null)
            : base(message, errorCode, null, innerException)
        {
        }
    }
}
