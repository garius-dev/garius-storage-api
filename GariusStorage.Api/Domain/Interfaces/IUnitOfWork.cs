namespace GariusStorage.Api.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        //IProdutoRepository Produtos { get; }
        //IClienteRepository Clientes { get; }
        //IPedidoRepository Pedidos { get; }
        // ...adicione os repositórios específicos que você criou

        Task<int> CommitAsync();
        Task RollbackAsync();
    }
}
