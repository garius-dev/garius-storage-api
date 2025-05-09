using Microsoft.AspNetCore.Identity;

namespace GariusStorage.Api.Domain.Entities.Identity
{
    // IdentityUser já inclui propriedades como Username, PasswordHash, Email, PhoneNumber, etc.
    public class ApplicationUser : IdentityUser<Guid> // Usamos int para o tipo da chave primária para consistência com suas outras entidades
    {
        // Adicione propriedades customizadas aqui, se necessário.
        // Exemplo:
        // public string FullName { get; set; }
        // public DateTime DateOfBirth { get; set; }

        // O IdentityUser já tem propriedades como EmailConfirmed, PhoneNumberConfirmed, LockoutEnabled, LockoutEnd, TwoFactorEnabled.
        // Se você precisar de um status de ativação simples como o seu 'Enabled', pode adicionar,
        // mas considere usar LockoutEnabled do Identity, que é mais robusto para bloqueio de contas.
        // public bool IsActive { get; set; } = true; // Exemplo de propriedade customizada

        // Construtor padrão necessário para Identity e EF Core
        public ApplicationUser() : base() { }

        // Construtor com username (email é comum ser o username)
        public ApplicationUser(string userName) : base(userName) { }
    }
}
