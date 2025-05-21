using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class SellerRepository : Repository<Seller>, ISellerRepository
    {
        public SellerRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de ISellerRepository aqui, se houver
    }
}
