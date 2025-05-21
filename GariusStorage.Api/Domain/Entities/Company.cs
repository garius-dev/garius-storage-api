using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Helpers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class Company : BaseEntity
    {
        [Required, MaxLength(255)]
        public string LegalName { get; set; }

        [MaxLength(255)]
        public string? TradeName { get; set; }

        [Required, MaxLength(18), CNPJ]
        public string CNPJ { get; set; }

        [MaxLength(50)]
        public string? StateRegistration { get; set; }

        [MaxLength(50)]
        public string? MunicipalRegistration { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? ImageUrl { get; set; }

        // Chave estrangeira para a moeda padrão
        public Guid? DefaultCurrencyId { get; set; }

        // Propriedade de navegação para a moeda padrão
        [ForeignKey("DefaultCurrencyId")]
        public Currency? DefaultCurrency { get; set; }

        public ICollection<ApplicationUser> Users { get; set; } = [];
        public ICollection<Product> Products { get; set; } = [];
        public ICollection<Customer> Customers { get; set; } = [];
        public ICollection<Stock> Stocks { get; set; } = [];
        public ICollection<StockMovement> StockMovements { get; set; } = [];
        public ICollection<CashFlow> CashFlows { get; set; } = [];
        public ICollection<Category> Categories { get; set; } = [];
        public ICollection<Supplier> Suppliers { get; set; } = [];
        public ICollection<Purchase> Purchases { get; set; } = [];
        public ICollection<PurchaseItem> PurchaseItems { get; set; } = [];
        public ICollection<Sale> Sales { get; set; } = [];
        public ICollection<SaleItem> SaleItems { get; set; } = [];
        public ICollection<Seller> Sellers { get; set; } = [];
        public ICollection<Invoice> Invoices { get; set; } = [];
        public ICollection<StorageLocation> StorageLocations { get; set; } = [];
        //public ICollection<Currencies> Currencies { get; set; } = new List<Currencies>();
    }
}
