using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
    {
        public InvoiceRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de IInvoiceRepository aqui, se houver
    }
}
