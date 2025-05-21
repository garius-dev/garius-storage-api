using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class PurchaseRepository : Repository<Purchase>, IPurchaseRepository
    {
        public PurchaseRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de IPurchaseRepository aqui, se houver
    }
}
