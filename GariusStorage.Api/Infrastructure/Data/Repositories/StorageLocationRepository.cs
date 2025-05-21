using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Infrastructure.Data.Repositories
{
    public class StorageLocationRepository : Repository<StorageLocation>, IStorageLocationRepository
    {
        public StorageLocationRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        // Implemente métodos específicos de IStorageLocationRepository aqui, se houver
    }
}
