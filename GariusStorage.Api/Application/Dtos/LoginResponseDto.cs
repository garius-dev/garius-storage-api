namespace GariusStorage.Api.Application.Dtos
{
    public class LoginResponseDto
    {
        public string Token { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
    }
}
