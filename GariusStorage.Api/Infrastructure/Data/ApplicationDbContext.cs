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

        // Propriedade para armazenar o CompanyId do tenant atual
        public Guid? CurrentCompanyId { get; private set; }

        // DbSets para as entidades de domínio
        public DbSet<CashFlows> CashFlows { get; set; }
        public DbSet<Categories> Categories { get; set; }
        public DbSet<Companies> Companies { get; set; }
        public DbSet<Currencies> Currencies { get; set; }
        public DbSet<Customers> Customers { get; set; }
        public DbSet<Invoices> Invoices { get; set; }
        public DbSet<Products> Products { get; set; }
        public DbSet<PurchaseItems> PurchaseItems { get; set; }
        public DbSet<Purchases> Purchases { get; set; }
        public DbSet<SaleItems> SaleItems { get; set; }
        public DbSet<Sales> Sales { get; set; }
        public DbSet<Sellers> Sellers { get; set; }
        public DbSet<StockMovements> StockMovements { get; set; }
        public DbSet<Stocks> Stocks { get; set; }
        public DbSet<StorageLocations> StorageLocations { get; set; }
        public DbSet<Suppliers> Suppliers { get; set; }

        // Construtor para uso em design-time (migrações) sem o resolver
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            // CurrentCompanyId permanecerá null, o que é útil para migrações
            // ou cenários onde o tenant não é aplicável.
        }

        // Construtor para uso em runtime com o resolver
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantResolverService tenantResolver) : base(options)
        {
            _tenantResolver = tenantResolver;
            CurrentCompanyId = _tenantResolver?.GetCurrentCompanyId();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da relação CompanyId para ApplicationUser
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Company)
                      .WithMany(c => c.Users) // Se Companies não tiver uma ICollection<ApplicationUser>
                      .HasForeignKey(u => u.CompanyId)
                      .OnDelete(DeleteBehavior.SetNull); // Se a empresa for deletada, CompanyId do usuário vira null
            });

            modelBuilder.Entity<Companies>(entity =>
            {
                entity.HasOne(c => c.DefaultCurrency) // A Company tem uma DefaultCurrency
                      .WithMany() // Uma Currency pode ser a DefaultCurrency para muitas Companies (sem propriedade de navegação inversa em Currencies para isso)
                      .HasForeignKey(c => c.DefaultCurrencyId) // A chave estrangeira em Companies
                      .IsRequired(false) // DefaultCurrencyId é anulável
                      .OnDelete(DeleteBehavior.SetNull); // Se a Currency for deletada, DefaultCurrencyId em Company vira null
            });

            // Configuração das relações e Filtros Globais para entidades ITenantEntity
            ConfigureTenantSpecificEntity<CashFlows>(modelBuilder, c => c.CashFlows);
            ConfigureTenantSpecificEntity<Categories>(modelBuilder, c => c.Categories);
            ConfigureTenantSpecificEntity<Customers>(modelBuilder, c => c.Customers);
            ConfigureTenantSpecificEntity<Invoices>(modelBuilder, c => c.Invoices);
            ConfigureTenantSpecificEntity<Products>(modelBuilder, c => c.Products);
            ConfigureTenantSpecificEntity<PurchaseItems>(modelBuilder, c => c.PurchaseItems);
            ConfigureTenantSpecificEntity<Purchases>(modelBuilder, c => c.Purchases);
            ConfigureTenantSpecificEntity<SaleItems>(modelBuilder, c => c.SaleItems);
            ConfigureTenantSpecificEntity<Sales>(modelBuilder, c => c.Sales);
            ConfigureTenantSpecificEntity<Sellers>(modelBuilder, c => c.Sellers);
            ConfigureTenantSpecificEntity<StockMovements>(modelBuilder, c => c.StockMovements);
            ConfigureTenantSpecificEntity<Stocks>(modelBuilder, c => c.Stocks);
            ConfigureTenantSpecificEntity<StorageLocations>(modelBuilder, c => c.StorageLocations);
            ConfigureTenantSpecificEntity<Suppliers>(modelBuilder, c => c.Suppliers);

            // Entidades que podem ser globais ou necessitam de tratamento especial (ex: Currencies)
            // Se Currencies também for por CompanyId, adicione-a ao ConfigureTenantSpecificEntity
            // modelBuilder.Entity<Currencies>().HasQueryFilter(c => c.CompanyId == CurrentCompanyId || CurrentCompanyId == null);


            // Configurações específicas adicionais que você já tinha ou precisa:

            // CashFlows (relações com Sale e Purchase)
            modelBuilder.Entity<CashFlows>(entity =>
            {
                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.CashFlows)
                    .HasForeignKey(d => d.SaleId)
                    .OnDelete(DeleteBehavior.Cascade); // Ou Restrict/SetNull conforme sua regra
                entity.HasOne(d => d.Purchase)
                    .WithMany(p => p.CashFlows)
                    .HasForeignKey(d => d.PurchaseId)
                    .OnDelete(DeleteBehavior.Cascade); // Ou Restrict/SetNull
            });

            // Categories (relação ParentCategory)
            modelBuilder.Entity<Categories>(entity =>
            {
                entity.HasOne(d => d.ParentCategory)
                    .WithMany() // Se Categories tiver ICollection<Categories> SubCategories, use .WithMany(p => p.SubCategories)
                    .HasForeignKey(d => d.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Invoices (relação com Sale)
            modelBuilder.Entity<Invoices>(entity =>
            {
                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.Invoices)
                    .HasForeignKey(d => d.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Products (relação com Category)
            modelBuilder.Entity<Products>(entity =>
            {
                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PurchaseItems (relações com Purchase e Product)
            modelBuilder.Entity<PurchaseItems>(entity =>
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
            modelBuilder.Entity<Purchases>(entity =>
            {
                entity.HasOne(d => d.Supplier)
                    .WithMany(p => p.Purchases)
                    .HasForeignKey(d => d.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SaleItems (relações com Sale e Product)
            modelBuilder.Entity<SaleItems>(entity =>
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
            modelBuilder.Entity<Sales>(entity =>
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
            modelBuilder.Entity<StockMovements>(entity =>
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
            modelBuilder.Entity<Stocks>(entity =>
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
            Expression<Func<Companies, IEnumerable<TEntity>?>> navigationExpression) // Updated nullability
            where TEntity : BaseEntity, ITenantEntity
        {
            modelBuilder.Entity<TEntity>(entity =>
            {
                entity.HasOne(e => e.Company)
                      .WithMany(navigationExpression) // Updated to match nullability
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade); // Cascade delete for tenant entities when Company is deleted

                entity.HasQueryFilter(e => e.CompanyId == CurrentCompanyId || CurrentCompanyId == null);
            });
        }

        private static void SetDecimalPrecision(ModelBuilder modelBuilder)
        {
            // CashFlows
            modelBuilder.Entity<CashFlows>().Property(p => p.Amount).HasColumnType("decimal(18,2)");

            // Invoices
            modelBuilder.Entity<Invoices>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Invoices>().Property(p => p.TaxAmount).HasColumnType("decimal(18,2)");

            // Products
            modelBuilder.Entity<Products>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Products>().Property(p => p.Cost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Products>().Property(p => p.TaxQuantityPerUnit).HasColumnType("decimal(18,4)");
            modelBuilder.Entity<Products>().Property(p => p.ICMS_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Products>().Property(p => p.PIS_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Products>().Property(p => p.COFINS_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Products>().Property(p => p.IPI_Rate).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Products>().Property(p => p.Weight).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Products>().Property(p => p.NetWeight).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Products>().Property(p => p.GrossWeight).HasColumnType("decimal(18,2)");

            // PurchaseItems
            modelBuilder.Entity<PurchaseItems>().Property(p => p.UnitCost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<PurchaseItems>().Property(p => p.TotalCost).HasColumnType("decimal(18,2)");

            // Purchases
            modelBuilder.Entity<Purchases>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");

            // SaleItems
            modelBuilder.Entity<SaleItems>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<SaleItems>().Property(p => p.TotalPrice).HasColumnType("decimal(18,2)");

            // Sales
            modelBuilder.Entity<Sales>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
        }
    }
}
