using GariusStorage.Api.Domain.Interfaces.Repositories;

namespace GariusStorage.Api.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        ICashFlowRepository CashFlows { get; }
        ICategoryRepository Categories { get; }
        ICompanyRepository Companies { get; }
        ICurrencyRepository Currencies { get; }
        ICustomerRepository Customers { get; }
        IInvoiceRepository Invoices { get; }
        IProductRepository Products { get; }
        IPurchaseItemRepository PurchaseItems { get; }
        IPurchaseRepository Purchases { get; }
        ISaleItemRepository SaleItems { get; }
        ISaleRepository Sales { get; }
        ISellerRepository Sellers { get; }
        IStockMovementRepository StockMovements { get; }
        IStockRepository Stocks { get; }
        IStorageLocationRepository StorageLocations { get; }
        ISupplierRepository Suppliers { get; }

        Task<int> CommitAsync();
        Task RollbackAsync();
    }
}
