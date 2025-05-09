namespace GariusStorage.Api.Application.Dtos.Auth
{
    public class LoginResponseDto
    {
        public string Token { get; set; }
        public Guid UserId { get; set; } // Confirmado como Guid
        public string Username { get; set; }
        public string Email { get; set; }
        public string Message { get; set; }
        public IList<string> Roles { get; set; }

        public LoginResponseDto()
        {
            Roles = new List<string>();
        }
    }
}
