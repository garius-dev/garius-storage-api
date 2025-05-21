using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class CompanyRepository : Repository<Company>, ICompanyRepository
    {
        public CompanyRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de ICompanyRepository aqui, se houver
    }
}
