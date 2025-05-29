namespace GariusStorage.Api.Application.Dtos.Company
{
    public class CompanyDto
    {
        public Guid Id { get; set; }
        public string LegalName { get; set; }
        public string? TradeName { get; set; }
        public string CNPJ { get; set; }
        public string? StateRegistration { get; set; }
        public string? MunicipalRegistration { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? DefaultCurrencyId { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
