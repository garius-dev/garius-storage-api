using GariusStorage.Api.Domain.Interfaces;
using System;

namespace GariusStorage.Api.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        //public IProdutoRepository Produtos { get; }
        //public IClienteRepository Clientes { get; }
        //public IPedidoRepository Pedidos { get; }

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CommitAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public Task RollbackAsync()
        {
            return Task.CompletedTask;
        }
    }
}
