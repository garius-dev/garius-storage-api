using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Helpers
{
    public class CNPJAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return ValidationResult.Success; // Permitir nulo, se for opcional

            var cnpj = value.ToString().Replace(".", "").Replace("/", "").Replace("-", "");
            if (cnpj.Length != 14 || !IsCnpjValid(cnpj))
                return new ValidationResult("Invalid CNPJ.");

            return ValidationResult.Success;
        }

        private bool IsCnpjValid(string cnpj)
        {
            int[] multiplier1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] multiplier2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            if (cnpj.All(c => c == cnpj[0])) return false; // Evita sequências iguais

            int sum = 0;
            for (int i = 0; i < 12; i++)
                sum += int.Parse(cnpj[i].ToString()) * multiplier1[i];
            int remainder = sum % 11;
            int digit1 = remainder < 2 ? 0 : 11 - remainder;

            sum = 0;
            for (int i = 0; i < 13; i++)
                sum += int.Parse(cnpj[i].ToString()) * multiplier2[i];
            remainder = sum % 11;
            int digit2 = remainder < 2 ? 0 : 11 - remainder;

            return cnpj.EndsWith($"{digit1}{digit2}");
        }
    }
}
