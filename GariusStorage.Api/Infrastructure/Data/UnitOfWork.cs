using GariusStorage.Api.Domain.Interfaces;
using GariusStorage.Api.Domain.Interfaces.Repositories;
using GariusStorage.Api.Infrastructure.Data.Repositories;
using System;

namespace GariusStorage.Api.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public ICashFlowRepository CashFlows { get; private set; }
        public ICategoryRepository Categories { get; private set; }
        public ICompanyRepository Companies { get; private set; }
        public ICurrencyRepository Currencies { get; private set; }
        public ICustomerRepository Customers { get; private set; }
        public IInvoiceRepository Invoices { get; private set; }
        public IProductRepository Products { get; private set; }
        public IPurchaseItemRepository PurchaseItems { get; private set; }
        public IPurchaseRepository Purchases { get; private set; }
        public ISaleItemRepository SaleItems { get; private set; }
        public ISaleRepository Sales { get; private set; }
        public ISellerRepository Sellers { get; private set; }
        public IStockMovementRepository StockMovements { get; private set; }
        public IStockRepository Stocks { get; private set; }
        public IStorageLocationRepository StorageLocations { get; private set; }
        public ISupplierRepository Suppliers { get; private set; }

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            CashFlows = new CashFlowRepository(_context);
            Categories = new CategoryRepository(_context);
            Companies = new CompanyRepository(_context);
            Currencies = new CurrencyRepository(_context);
            Customers = new CustomerRepository(_context);
            Invoices = new InvoiceRepository(_context);
            Products = new ProductRepository(_context);
            PurchaseItems = new PurchaseItemRepository(_context);
            Purchases = new PurchaseRepository(_context);
            SaleItems = new SaleItemRepository(_context);
            Sales = new SaleRepository(_context);
            Sellers = new SellerRepository(_context);
            StockMovements = new StockMovementRepository(_context);
            Stocks = new StockRepository(_context);
            StorageLocations = new StorageLocationRepository(_context);
            Suppliers = new SupplierRepository(_context);
        }

        public async Task<int> CommitAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public Task RollbackAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
        }
    }
}
