using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class StockMovementRepository : Repository<StockMovement>, IStockMovementRepository
    {
        public StockMovementRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de IStockMovementRepository aqui, se houver
    }
}
