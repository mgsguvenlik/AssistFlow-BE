using Core.Enums.Crm;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// CRM Satın Alma workflow görev kaydı.
    ///
    /// Bir workflow adımının hangi kullanıcı veya rol tarafından
    /// işlem görmesi gerektiğini tutar.
    /// </summary>
    [Table("PurchaseRequestTask", Schema = "crm")]
    public class PurchaseRequestTask : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }


        #region Purchase Request

        public long PurchaseRequestId { get; set; }

        public PurchaseRequest PurchaseRequest { get; set; } = default!;

        #endregion


        #region Step

        /// <summary>
        /// Görevin ait olduğu CRM workflow adımı.
        /// </summary>
        public long PurchaseRequestStepId { get; set; }

        public PurchaseRequestStep PurchaseRequestStep { get; set; } = default!;

        #endregion


        #region Assignment

        /// <summary>
        /// Görev doğrudan bir kullanıcıya atanmışsa kullanılır.
        ///
        /// Örn:
        /// Talep Eden
        /// İlgili Yönetici
        /// </summary>
        public long? AssignedUserId { get; set; }

        public User? AssignedUser { get; set; }


        /// <summary>
        /// Görev bir role atanmışsa kullanılır.
        ///
        /// Örn:
        /// Satın Alma
        /// Depo
        /// Muhasebe
        /// </summary>
        public long? AssignedRoleId { get; set; }

        public Role? AssignedRole { get; set; }

        #endregion


        /// <summary>
        /// Görevin mevcut durumu.
        /// </summary>
        public PurchaseRequestTaskStatus Status { get; set; } = PurchaseRequestTaskStatus.Pending;


        #region Completion

        /// <summary>
        /// Görevin tamamlandığı tarih.
        /// </summary>
        public DateTimeOffset? CompletedDate { get; set; }


        /// <summary>
        /// Görev bir role atanmış olsa bile
        /// işlemi gerçekleştiren gerçek kullanıcı burada tutulur.
        /// </summary>
        public long? CompletedUserId { get; set; }

        public User? CompletedUser { get; set; }

        #endregion
    }
}