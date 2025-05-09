namespace GariusStorage.Api.Application.Exceptions
{
    public class NotFoundException : BaseApplicationException
    {
        public NotFoundException(string message, string? errorCode = null, Exception? innerException = null)
            : base(message, errorCode, null, innerException)
        {
        }

        public NotFoundException(string entityName, object key, string? errorCode = null, Exception? innerException = null)
            : base($"Entidade '{entityName}' com chave '{key}' não encontrada.", errorCode, null, innerException)
        {
        }
    }
}
