using Core.Enums.Crm;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// CRM Satın Alma Talebi ana kaydı.
    /// </summary>

    [Table("PurchaseRequest", Schema = "crm")]
    public class PurchaseRequest : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Kullanıcıya gösterilecek benzersiz talep numarası.
        /// Örn: SAT-2026-000001
        /// </summary>
        public string RequestNo { get; set; } = string.Empty;


        #region Tenant

        /// <summary>
        /// Talebin ait olduğu tenant.
        /// </summary>
        public long? TenantId { get; set; }

        public Tenant? Tenant { get; set; }

        #endregion


        #region Request Users

        /// <summary>
        /// Talebin sahibi.
        ///
        /// CreatedUser audit bilgisinden ayrı tutulur.
        /// Çünkü talep sahibi business anlam taşıyan bir bilgidir.
        /// </summary>
        public long RequesterUserId { get; set; }

        public User? RequesterUser { get; set; }


        /// <summary>
        /// Talebi değerlendirecek ilgili yönetici.
        /// </summary>
        public long? ManagerUserId { get; set; }

        public User? ManagerUser { get; set; }

        #endregion


        #region Request Information

        /// <summary>
        /// Talep konusu.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Talep sahibinin ana açıklaması.
        ///
        /// Workflow içerisindeki diğer kullanıcı açıklamaları
        /// PurchaseRequestHistory üzerinde tutulacaktır.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Satın Alma veya Araştırma ve Teklif.
        /// </summary>
        public PurchaseRequestType RequestType { get; set; }

        /// <summary>
        /// Ofis içi satın alma mı?
        /// </summary>
        public bool IsOfficePurchase { get; set; }

        #endregion


        #region Customer

        /// <summary>
        /// Talep belirli bir müşteriyle ilişkiliyse kullanılır.
        /// </summary>
        public long? CustomerId { get; set; }

        public Customer? Customer { get; set; }

        #endregion


        #region System Type

        /// <summary>
        /// Satın alma talebinin ilişkili olduğu sistem tipi.
        /// Mevcut ortak SystemType tablosu kullanılacaktır.
        /// </summary>
        public long? SystemTypeId { get; set; }

        public SystemType? SystemType { get; set; }

        #endregion


        #region Process

        /// <summary>
        /// Talebin genel business durumu.
        ///
        /// Workflow adımı değildir.
        /// </summary>
        public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;


        /// <summary>
        /// Talebin CRM satın alma workflow'unda
        /// bulunduğu mevcut adım.
        /// </summary>
        public long? CurrentStepId { get; set; }

        public PurchaseRequestStep? CurrentStep { get; set; }


        /// <summary>
        /// Talep Completed, Rejected veya Cancelled olduğunda
        /// kapanış tarihi tutulur.
        /// </summary>
        public DateTimeOffset? ClosedDate { get; set; }

        #endregion


        #region Navigations

        /// <summary>
        /// Talepteki ürün/hizmet kalemleri.
        /// </summary>
        public ICollection<PurchaseRequestItem> Items { get; set; } = new List<PurchaseRequestItem>();


        /// <summary>
        /// Talebin görev kayıtları.
        /// </summary>
        public ICollection<PurchaseRequestTask> Tasks { get; set; } = new List<PurchaseRequestTask>();


        /// <summary>
        /// Talebin süreç hareket geçmişi.
        /// </summary>
        public ICollection<PurchaseRequestHistory> Histories { get; set; } = new List<PurchaseRequestHistory>();

        /// <summary>
        /// Talebe eklenen dosyalar.
        /// </summary>
        public ICollection<PurchaseAttachment> Attachments { get; set; } = new List<PurchaseAttachment>();

        #endregion
    }
}