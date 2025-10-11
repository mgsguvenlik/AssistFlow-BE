using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    // Product (Ürün)
    public class Product : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        public string? ProductCode { get; set; }          // Ürün Kodu
        public string? OracleProductCode { get; set; }    // Oracle Kodu (ürün)
        public string? SystemType { get; set; }           // Sistem Tipi (serbest metin)

        public long? BrandId { get; set; }                // Marka
        public Brand? Brand { get; set; }

        public long? ModelId { get; set; }                // Model
        public Model? Model { get; set; }

        public string? Description { get; set; }          // Tanımı
        public string? PriceCurrency { get; set; }        // Fiyat Para Birimi (örn: TRY)
        public decimal? Price { get; set; }               // Fiyat

        public long? CurrencyTypeId { get; set; }         // Döviz Türü Id
        public CurrencyType? CurrencyType { get; set; }

        public DateTimeOffset? InstallationDate { get; set; } // Kurulum (varsayım #1)
        public DateTimeOffset? ConnectionDate { get; set; }   // Bağlantı (varsayım #1)

        public string? CorporateCustomerShortCode { get; set; } // Kurumsal Müşteri Kısa Kodu
        public string? OracleCustomerCode { get; set; }          // Oracle Kodu (müşteri, varsayım #2)

        public long? ProductTypeId { get; set; }            // Ürün Tipi Id
        public ProductType? ProductType { get; set; }

        // Navigations (fiyatlar)
        public ICollection<CustomerProductPrice> CustomerProductPrices { get; set; } = new List<CustomerProductPrice>();
        public ICollection<CustomerGroupProductPrice> GroupProductPrices { get; set; } = new List<CustomerGroupProductPrice>();
    }
}
