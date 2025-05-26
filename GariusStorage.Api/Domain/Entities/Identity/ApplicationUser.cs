using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities.Identity
{

    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsExternalUser { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool IsCompanyOwner { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }

        // Propriedades para Multi-Tenancy
        public Guid? CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company? Company { get; set; }

        public ApplicationUser() : base()
        {
            CreatedAt = DateTime.UtcNow;
            LastUpdate = DateTime.UtcNow;
        }

        public ApplicationUser(string userName) : base(userName)
        {
            CreatedAt = DateTime.UtcNow;
            LastUpdate = DateTime.UtcNow;
        }
    }
}
