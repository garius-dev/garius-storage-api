using System.ComponentModel.DataAnnotations;

namespace GariusStorage.Api.Helpers
{
    public class CPFAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return ValidationResult.Success; // Permitir nulo, se for opcional

            var cpf = value.ToString().Replace(".", "").Replace("-", "");
            if (cpf.Length != 11 || !IsCpfValid(cpf))
                return new ValidationResult("Invalid CPF.");

            return ValidationResult.Success;
        }

        private bool IsCpfValid(string cpf)
        {
            int[] multiplier1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] multiplier2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            if (cpf.All(c => c == cpf[0])) return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += int.Parse(cpf[i].ToString()) * multiplier1[i];
            int remainder = sum % 11;
            int digit1 = remainder < 2 ? 0 : 11 - remainder;

            sum = 0;
            for (int i = 0; i < 10; i++)
                sum += int.Parse(cpf[i].ToString()) * multiplier2[i];
            remainder = sum % 11;
            int digit2 = remainder < 2 ? 0 : 11 - remainder;

            return cpf.EndsWith($"{digit1}{digit2}");
        }
    }
}
