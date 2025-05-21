using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Entities;
using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Domain.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GariusStorage.Api.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        private readonly ITenantResolverService? _tenantResolver;
        public Guid? CurrentCompanyId { get; private set; }

        public DbSet<CashFlow> CashFlows { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Seller> Sellers { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<StorageLocation> StorageLocations { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantResolverService tenantResolver) : base(options)
        {
            _tenantResolver = tenantResolver;
            CurrentCompanyId = _tenantResolver?.GetCurrentCompanyId();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Company)
                      .WithMany(c => c.Users)
                      .HasForeignKey(u => u.CompanyId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasOne(c => c.DefaultCurrency)
                      .WithMany()
                      .HasForeignKey(c => c.DefaultCurrencyId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });


            ConfigureTenantSpecificEntity<CashFlow>(modelBuilder, c => c.CashFlows);
            ConfigureTenantSpecificEntity<Category>(modelBuilder, c => c.Categories);
            ConfigureTenantSpecificEntity<Customer>(modelBuilder, c => c.Customers);
            ConfigureTenantSpecificEntity<Invoice>(modelBuilder, c => c.Invoices);
            ConfigureTenantSpecificEntity<Product>(modelBuilder, c => c.Products);
            ConfigureTenantSpecificEntity<PurchaseItem>(modelBuilder, c => c.PurchaseItems);
            ConfigureTenantSpecificEntity<Purchase>(modelBuilder, c => c.Purchases);
            ConfigureTenantSpecificEntity<SaleItem>(modelBuilder, c => c.SaleItems);
            ConfigureTenantSpecificEntity<Sale>(modelBuilder, c => c.Sales);
            ConfigureTenantSpecificEntity<Seller>(modelBuilder, c => c.Sellers);
            ConfigureTenantSpecificEntity<StockMovement>(modelBuilder, c => c.StockMovements);
            ConfigureTenantSpecificEntity<Stock>(modelBuilder, c => c.Stocks);
            ConfigureTenantSpecificEntity<StorageLocation>(modelBuilder, c => c.StorageLocations);
            ConfigureTenantSpecificEntity<Supplier>(modelBuilder, c => c.Suppliers);


            modelBuilder.Entity<CashFlow>(entity =>
            {
                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.CashFlows)
                    .HasForeignKey(d => d.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.Purchase)
                    .WithMany(p => p.CashFlows)
                    .HasForeignKey(d => d.PurchaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Categories (relação ParentCategory)
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasOne(d => d.ParentCategory)
                    .WithMany()
                    .HasForeignKey(d => d.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Invoices (relação com Sale)
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.Invoices)
                    .HasForeignKey(d => d.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Products (relação com Category)
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PurchaseItems (relações com Purchase e Product)
            modelBuilder.Entity<PurchaseItem>(entity =>
            {
                entity.HasOne(d => d.Purchase)
                    .WithMany(p => p.Items)
                    .HasForeignKey(d => d.PurchaseId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.Product)
                    .WithMany()
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Purchases (relação com Supplier)
            modelBuilder.Entity<Purchase>(entity =>
            {
                entity.HasOne(d => d.Supplier)
                    .WithMany(p => p.Purchases)
                    .HasForeignKey(d => d.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SaleItems (relações com Sale e Product)
            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.Items)
                    .HasForeignKey(d => d.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.Product)
                    .WithMany()
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Sales (relações com Seller e Customer)
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasOne(d => d.Seller)
                    .WithMany(p => p.Sales)
                    .HasForeignKey(d => d.SellerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.Sales)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // StockMovements (relações com Product, Sale, Purchase)
            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasOne(d => d.Product)
                    .WithMany()
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.StockMovements)
                    .HasForeignKey(d => d.SaleId)
                    .IsRequired(false) // Permite SaleId nulo
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.Purchase)
                    .WithMany(p => p.StockMovements)
                    .HasForeignKey(d => d.PurchaseId)
                    .IsRequired(false) // Permite PurchaseId nulo
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Stocks (relações com Product e StorageLocation)
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasOne(d => d.Product)
                    .WithMany()
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.StorageLocation)
                    .WithMany(p => p.Stocks)
                    .HasForeignKey(d => d.StorageLocationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Decimal precision for all relevant properties
            SetDecimalPrecision(modelBuilder);
        }

        private void ConfigureTenantSpecificEntity<TEntity>(
            ModelBuilder modelBuilder,
            Expression<Func<Company, IEnumerable<TEntity>?>> navigationExpression)
            where TEntity : BaseEntity, ITenantEntity
        {
            modelBuilder.Entity<TEntity>(entity =>
            {
                entity.HasOne(e => e.Company)
                      .WithMany(navigationExpression)
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => e.CompanyId == CurrentCompanyId || CurrentCompanyId == null);
            });
        }

        private static void SetDecimalPrecision(ModelBuilder modelBuilder)
        {
            // CashFlows
            modelBuilder.Entity<CashFlow>().Property(p => p.Amount).HasColumnType("decimal(18,2)");

            // Invoices
            modelBuilder.Entity<Invoice>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Invoice>().Property(p => p.TaxAmount).HasColumnType("decimal(18,2)");

            // Products
            modelBuilder.Entity<Product>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>().Property(p => p.Cost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>().Property(p => p.TaxQuantityPerUnit).HasColumnType("decimal(18,4)");
            modelBuilder.Entity<Product>().Property(p => p.ICMS_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Product>().Property(p => p.PIS_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Product>().Property(p => p.COFINS_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Product>().Property(p => p.IPI_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Product>().Property(p => p.Weight).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>().Property(p => p.NetWeight).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>().Property(p => p.GrossWeight).HasColumnType("decimal(18,2)");

            // PurchaseItems
            modelBuilder.Entity<PurchaseItem>().Property(p => p.UnitCost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<PurchaseItem>().Property(p => p.TotalCost).HasColumnType("decimal(18,2)");

            // Purchases
            modelBuilder.Entity<Purchase>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");

            // SaleItems
            modelBuilder.Entity<SaleItem>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<SaleItem>().Property(p => p.TotalPrice).HasColumnType("decimal(18,2)");

            // Sales
            modelBuilder.Entity<Sale>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
        }
    }
}
