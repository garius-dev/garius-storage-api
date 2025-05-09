namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class AuthResult
    {
        public bool Succeeded { get; private set; }
        public LoginResponseDto? LoginResponse { get; private set; }
        public IEnumerable<string> Errors { get; private set; }
        public bool IsLockedOut { get; private set; }
        public bool IsNotAllowed { get; private set; }
        public bool RequiresTwoFactor { get; private set; } // Para futuras implementações de 2FA

        private AuthResult(bool succeeded, LoginResponseDto? loginResponse, IEnumerable<string>? errors, bool isLockedOut = false, bool isNotAllowed = false, bool requiresTwoFactor = false)
        {
            Succeeded = succeeded;
            LoginResponse = loginResponse;
            Errors = errors ?? Enumerable.Empty<string>();
            IsLockedOut = isLockedOut;
            IsNotAllowed = isNotAllowed;
            RequiresTwoFactor = requiresTwoFactor;
        }

        public static AuthResult Success(LoginResponseDto loginResponse)
        {
            return new AuthResult(true, loginResponse, null);
        }

        public static AuthResult Failed(params string[] errors)
        {
            return new AuthResult(false, null, errors);
        }

        public static AuthResult Failed(IEnumerable<string> errors)
        {
            return new AuthResult(false, null, errors);
        }

        public static AuthResult LockedOut()
        {
            return new AuthResult(false, null, new[] { "Conta bloqueada devido a múltiplas tentativas de login falhas." }, isLockedOut: true);
        }

        public static AuthResult NotAllowed(string errorMessage)
        {
            return new AuthResult(false, null, new[] { errorMessage }, isNotAllowed: true);
        }
        public static AuthResult RequiresTwoFactorAuth()
        {
            return new AuthResult(false, null, new[] { "Autenticação de dois fatores é necessária." }, requiresTwoFactor: true);
        }
    }
}
