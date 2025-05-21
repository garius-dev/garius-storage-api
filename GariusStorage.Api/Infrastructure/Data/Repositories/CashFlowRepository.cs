using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class CashFlowRepository : Repository<CashFlow>, ICashFlowRepository
    {
        public CashFlowRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de ICashFlowRepository aqui, se houver
    }
}
