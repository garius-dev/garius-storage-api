using Microsoft.AspNetCore.Identity;

namespace GariusStorage.Api.Domain.Entities.Identity
{
    public class ApplicationRole : IdentityRole<Guid> // Usamos int para o tipo da chave primária
    {
        // Adicione propriedades customizadas para roles aqui, se necessário.
        // Exemplo:
        // public string Description { get; set; }

        public bool IsExternalUser { get; set; } = false;

        // Construtor padrão necessário para Identity e EF Core
        public ApplicationRole() : base() { }

        // Construtor com nome da role
        public ApplicationRole(string roleName) : base(roleName) { }
    }
}
