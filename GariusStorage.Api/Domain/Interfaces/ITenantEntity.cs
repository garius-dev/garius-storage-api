using GariusStorage.Api.Domain.Entities;

namespace GariusStorage.Api.Domain.Interfaces
{
    public interface ITenantEntity
    {
        Guid CompanyId { get; set; }
        Company Company { get; set; }
    }
}
