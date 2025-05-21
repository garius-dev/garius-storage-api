using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class PurchaseItemRepository : Repository<PurchaseItem>, IPurchaseItemRepository
    {
        public PurchaseItemRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de IPurchaseItemRepository aqui, se houver
    }
}
