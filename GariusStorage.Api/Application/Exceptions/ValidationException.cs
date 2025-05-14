using FluentValidation.Results;

namespace GariusStorage.Api.Application.Exceptions
{
    public class ValidationException : BaseApplicationException
    {
        // Construtor principal para mensagem e errorCode, opcionalmente com detalhes.
        public ValidationException(string message, string? errorCode = null, Dictionary<string, string[]>? details = null, Exception? innerException = null)
            : base(message, errorCode, details ?? new Dictionary<string, string[]>(), innerException)
        {
        }

        // Construtor para quando se tem apenas uma mensagem e um errorCode (sem detalhes de campo específicos inicialmente).
        // Este é o que parece estar sendo usado no AuthService: new ValidationException("mensagem", "CODIGO_ERRO")
        public ValidationException(string message, string? errorCode)
            : this(message, errorCode, null, null)
        {
        }

        // Construtor que aceita uma lista de ValidationFailure do FluentValidation.
        // Permite passar uma mensagem geral e um errorCode para o grupo de falhas.
        public ValidationException(
            IEnumerable<ValidationFailure> failures,
            string message = "Uma ou mais falhas de validação ocorreram.",
            string? errorCode = null)
            : base(message, errorCode, failures?.GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                                             .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray())
                                        ?? new Dictionary<string, string[]>())
        {
            // O Details é preenchido pela chamada ao construtor base.
        }

        // Construtor que aceita um dicionário de erros de validação.
        // Permite passar uma mensagem geral e um errorCode.
        public ValidationException(
            Dictionary<string, string[]> validationErrors,
            string message = "Uma ou mais falhas de validação ocorreram.",
            string? errorCode = null)
            : base(message, errorCode, validationErrors)
        {
        }

        // Construtor apenas com mensagem (mantido para compatibilidade, mas errorCode será null).
        public ValidationException(string message)
            : this(message, null, null, null)
        {
        }

        // Construtor padrão (mantido para compatibilidade, mas errorCode será null).
        public ValidationException()
            : this("Uma ou mais falhas de validação ocorreram.", null, null, null)
        {
        }
    }
}
