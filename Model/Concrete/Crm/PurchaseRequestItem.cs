using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// Satın Alma Talebi ürün/hizmet kalemi.
    /// </summary>
    [Table("PurchaseRequestItem", Schema = "crm")]
    public class PurchaseRequestItem : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }


        #region Purchase Request

        /// <summary>
        /// Bağlı olduğu satın alma talebi.
        /// </summary>
        public long PurchaseRequestId { get; set; }

        public PurchaseRequest PurchaseRequest { get; set; } = default!;


        /// <summary>
        /// Talep içerisindeki kalem sıra numarası.
        /// </summary>
        public int LineNo { get; set; }

        #endregion


        #region Product

        /// <summary>
        /// Ortak Product tablosundaki ürün.
        ///
        /// Ürün kartı olmayan serbest ürün girişinde null olabilir.
        /// </summary>
        public long? ProductId { get; set; }

        public Product? Product { get; set; }


        /// <summary>
        /// Talep sırasında kullanılan ürün adı.
        ///
        /// ProductId olmayan ürünlerde serbest ürün adı olarak kullanılır.
        /// </summary>
        public string? ProductName { get; set; }


        /// <summary>
        /// Talep edilen miktar.
        /// </summary>
        public decimal Quantity { get; set; }


        /// <summary>
        /// Talep anındaki marka bilgisi.
        /// </summary>
        public string? BrandName { get; set; }


        /// <summary>
        /// Talep anındaki model bilgisi.
        /// </summary>
        public string? ModelName { get; set; }


        /// <summary>
        /// Kaleme ait açıklama.
        /// </summary>
        public string? Description { get; set; }

        #endregion


        #region Alternate Product

        /// <summary>
        /// Satın alma araştırmasında bulunan muadil ürün.
        /// </summary>
        public long? AlternateProductId { get; set; }

        public Product? AlternateProduct { get; set; }


        /// <summary>
        /// Muadil ürün kayıtlı değilse serbest ürün adı.
        /// </summary>
        public string? AlternateProductName { get; set; }

        #endregion


        #region Supplier

        /// <summary>
        /// Talep kalemi için seçilen tedarikçi.
        ///
        /// Şimdilik serbest metin olarak tutulacaktır.
        /// İleride Supplier master yapısı oluşturulursa
        /// SupplierId eklenebilir.
        /// </summary>
        public string? SupplierName { get; set; }

        #endregion


        #region Pricing

        /// <summary>
        /// Tedarikçinin liste fiyatı.
        /// </summary>
        public decimal? SupplierListPrice { get; set; }


        /// <summary>
        /// Tedarikçi indirim oranı.
        ///
        /// %15 için 15 tutulur.
        /// </summary>
        public decimal? SupplierDiscountRate { get; set; }


        /// <summary>
        /// İndirim sonrası net satın alma fiyatı.
        /// </summary>
        public decimal? SupplierNetPrice { get; set; }


        /// <summary>
        /// Ortak CurrencyType tablosu.
        /// </summary>
        public long? CurrencyTypeId { get; set; }

        public CurrencyType? CurrencyType { get; set; }

        #endregion


        #region Procurement

        /// <summary>
        /// Tedarikçi stok durumu.
        ///
        /// Örn:
        /// Stokta
        /// Terminli
        /// Stok Yok
        /// </summary>
        public string? StockStatus { get; set; }


        /// <summary>
        /// Vade bilgisi.
        ///
        /// Örn:
        /// Peşin
        /// 30 Gün
        /// 60 Gün
        /// </summary>
        public string? Maturity { get; set; }


        /// <summary>
        /// Cari / firma kodu.
        /// </summary>
        public string? CompanyCode { get; set; }


        /// <summary>
        /// Bu kalemin fiziksel depo kontrolüne gitmesi gerekiyor mu?
        /// </summary>
        public bool RequiresWarehouseControl { get; set; }

        #endregion


        #region Confirmation

        /// <summary>
        /// Kalem bazlı değerlendirme sonucu.
        ///
        /// null  : Henüz değerlendirilmedi
        /// true  : Uygun
        /// false : Uygun değil
        /// </summary>
        public bool? IsConfirmed { get; set; }

        #endregion
    }
}