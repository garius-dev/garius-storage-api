using GariusStorage.Api.Domain.Entities.Identity;
using GariusStorage.Api.Helpers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GariusStorage.Api.Domain.Entities
{
    public class Companies : BaseEntity
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
        public Currencies? DefaultCurrency { get; set; }

        public ICollection<ApplicationUser> Users { get; set; } = [];
        public ICollection<Products> Products { get; set; } = [];
        public ICollection<Customers> Customers { get; set; } = [];
        public ICollection<Stocks> Stocks { get; set; } = [];
        public ICollection<StockMovements> StockMovements { get; set; } = [];
        public ICollection<CashFlows> CashFlows { get; set; } = [];
        public ICollection<Categories> Categories { get; set; } = [];
        public ICollection<Suppliers> Suppliers { get; set; } = [];
        public ICollection<Purchases> Purchases { get; set; } = [];
        public ICollection<PurchaseItems> PurchaseItems { get; set; } = [];
        public ICollection<Sales> Sales { get; set; } = [];
        public ICollection<SaleItems> SaleItems { get; set; } = [];
        public ICollection<Sellers> Sellers { get; set; } = [];
        public ICollection<Invoices> Invoices { get; set; } = [];
        public ICollection<StorageLocations> StorageLocations { get; set; } = [];
        //public ICollection<Currencies> Currencies { get; set; } = new List<Currencies>();
    }
}
