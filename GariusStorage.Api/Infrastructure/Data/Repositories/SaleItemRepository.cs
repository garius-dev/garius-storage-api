using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class SaleItemRepository : Repository<SaleItem>, ISaleItemRepository
    {
        public SaleItemRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de ISaleItemRepository aqui, se houver
    }
}
