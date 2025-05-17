using GariusStorage.Api.Helpers;
using System.ComponentModel.DataAnnotations;

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


        public ICollection<Products> Products { get; set; } = new List<Products>();
        public ICollection<Stocks> Stocks { get; set; } = new List<Stocks>();
        public ICollection<StockMovements> StockMovements { get; set; } = new List<StockMovements>();
        public ICollection<CashFlows> CashFlows { get; set; } = new List<CashFlows>();
        public ICollection<Categories> Categories { get; set; } = new List<Categories>();
        public ICollection<Suppliers> Suppliers { get; set; } = new List<Suppliers>();
        public ICollection<Purchases> Purchases { get; set; } = new List<Purchases>();
        public ICollection<Sales> Sales { get; set; } = new List<Sales>();
        public ICollection<Sellers> Sellers { get; set; } = new List<Sellers>();
        public ICollection<Invoices> Invoices { get; set; } = new List<Invoices>();
        public ICollection<StorageLocations> StorageLocations { get; set; } = new List<StorageLocations>();
        public ICollection<Currencies> Currencies { get; set; } = new List<Currencies>();
    }
}
