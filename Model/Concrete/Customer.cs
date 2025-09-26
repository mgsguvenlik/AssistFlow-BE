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
        /// Müşterinin bağlı olduğu ana grup adı (segment/kategori).
        /// </summary>
        public string? CustomerMainGroupName { get; set; }

        /// <summary>
        /// Abonenin açık adresi (cadde, mahalle, no vb.).
        /// </summary>
        public string? SubscriberAddress { get; set; }

        /// <summary>
        /// Şehir/İl bilgisi (örn. İzmir).
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Lokasyon tanımlayıcı kodu (iç sistemlerdeki Lokasyon ID/Code).
        /// </summary>
        public string? LocationCode { get; set; }

        /// <summary>
        /// Oracle sistemindeki karşılık gelen kod (varsa).
        /// </summary>
        public string? OracleCode { get; set; }

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

        /// <summary>
        /// Müşteri tipi kimliği (örn. B2B, B2C, bayi vb. türleri için referans ID).
        /// </summary>
        [ForeignKey(nameof(CustomerType))]
        public long? CustomerTypeId { get; set; }
        public CustomerType? CustomerType { get; set; }
    }
}
