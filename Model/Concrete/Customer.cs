using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    public class Customer : AuditableWithUserEntity
    {
        /// <summary>
        /// Birincil anahtar (PK). Veritabanında benzersiz kimlik.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Abone/Müşteri benzersiz kodu (ör. SAP/CRM’deki müşteri no).
        /// </summary>
        public string? SubscriberCode { get; set; }

        /// <summary>
        /// Abone/Müşteri firma adı (ticari unvan).
        /// </summary>
        public string? SubscriberCompany { get; set; }


        /// <summary>
        /// Abonenin açık adresi (cadde, mahalle, no vb.).
        /// </summary>
        public string? SubscriberAddress { get; set; }

        /// <summary>
        /// Şehir/İl bilgisi (örn. İzmir).
        /// </summary>
        public string? City { get; set; }


        /// <summary>
        /// 
        /// </summary>
        public string? District { get; set; }

        /// <summary>
        /// Lokasyon tanımlayıcı kodu (iç sistemlerdeki Lokasyon ID/Code).
        /// </summary>
        public string? LocationCode { get; set; }

        /// <summary>
        /// Birincil iletişim kişisinin adı-soyadı.
        /// </summary>
        public string? ContactName1 { get; set; }

        /// <summary>
        /// Birincil iletişim kişisinin telefon numarası.
        /// </summary>
        public string? Phone1 { get; set; }

        /// <summary>
        /// Birincil iletişim kişisinin e-posta adresi.
        /// </summary>
        public string? Email1 { get; set; }

        /// <summary>
        /// İkincil iletişim kişisinin adı-soyadı.
        /// </summary>
        public string? ContactName2 { get; set; }

        /// <summary>
        /// İkincil iletişim kişisinin telefon numarası.
        /// </summary>
        public string? Phone2 { get; set; }

        /// <summary>
        /// İkincil iletişim kişisinin e-posta adresi.
        /// </summary>
        public string? Email2 { get; set; }

        /// <summary>
        /// Müşterinin kısa kodu/kısaltması (rapor ve arama amaçlı).
        /// </summary>
        public string? CustomerShortCode { get; set; }

        /// <summary>
        /// Kurumsal lokasyon ID (kurumsal ağdaki/harici sistemlerdeki lokasyon referansı).
        /// </summary>
        public string? CorporateLocationId { get; set; }

        public string? Longitude { get; set; }
        public string? Latitude { get; set; }

        /// <summary>
        /// Kurulum tarihi 
        /// </summary>
        public DateTimeOffset? InstallationDate { get; set; }

        /// <summary>
        /// Garanti süresi (yıl). Null ise garanti takibi yok kabul edilir.
        /// Örn: 1, 2, 3...
        /// </summary>
        public int? WarrantyYears { get; set; }


        public string? Note {  get; set; }

        // 🔹 Yeni kolonlar
        public string? LockType { get; set; }
        public string? CashCenter { get; set; }

        /// <summary>
        /// </summary>
        [ForeignKey(nameof(CustomerGroup))]
        public long? CustomerGroupId { get; set; }
        public CustomerGroup? CustomerGroup { get; set; }
    

        [ForeignKey(nameof(CustomerType))]
        public long? CustomerTypeId { get; set; }
        public CustomerType? CustomerType { get; set; }



        // Navigations (fiyatlar)
        public ICollection<CustomerProductPrice> CustomerProductPrices { get; set; } = new List<CustomerProductPrice>();

        public ICollection<CustomerSystemAssignment> CustomerSystemAssignments { get; set; }  = new List<CustomerSystemAssignment>();



    }
}
