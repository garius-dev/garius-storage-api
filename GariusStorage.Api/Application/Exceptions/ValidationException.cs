using FluentValidation.Results;

namespace GariusStorage.Api.Application.Exceptions
{
    public class ValidationException : BaseApplicationException
    {
        public ValidationException(string message = "Uma ou mais falhas de validação ocorreram.")
            : base(message)
        {
            Details ??= new Dictionary<string, string[]>();
        }

        public ValidationException(IEnumerable<ValidationFailure> failures)
            : this()
        {

            Details ??= new Dictionary<string, string[]>();

            // Mapeia falhas do FluentValidation para o dicionário de detalhes
            var failureGroups = failures
                .GroupBy(e => e.PropertyName, e => e.ErrorMessage);

            foreach (var failureGroup in failureGroups)
            {
                var propertyName = failureGroup.Key;
                var errorMessages = failureGroup.ToArray();
                Details.Add(propertyName, errorMessages);
            }
        }

        public ValidationException(Dictionary<string, string[]> validationErrors)
           : this()
        {
            Details = validationErrors;
        }
    }
}
