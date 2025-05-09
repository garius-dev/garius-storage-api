using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace GariusStorage.Api.Domain.Entities.Identity
{

    public class ApplicationUser : IdentityUser<Guid>
    {

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsExternalUser { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }

        public ApplicationUser() : base() 
        {
            CreatedAt = DateTime.UtcNow;
            LastUpdate = DateTime.UtcNow;
        }

        // Construtor com username (email é comum ser o username)
        public ApplicationUser(string userName) : base(userName)
        {
            CreatedAt = DateTime.UtcNow;
            LastUpdate = DateTime.UtcNow;
        }
    }
}
