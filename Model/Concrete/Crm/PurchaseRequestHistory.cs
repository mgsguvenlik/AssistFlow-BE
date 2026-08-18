using Core.Enums.Crm;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// CRM Satın Alma süreç hareket geçmişi.
    /// </summary>
    [Table("PurchaseRequestHistory", Schema = "crm")]
    public class PurchaseRequestHistory : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }


        #region Purchase Request

        public long PurchaseRequestId { get; set; }

        public PurchaseRequest PurchaseRequest { get; set; } = default!;

        #endregion


        #region From Step

        /// <summary>
        /// İşlem öncesindeki workflow adımı.
        ///
        /// İlk kayıt gibi durumlarda null olabilir.
        /// </summary>
        public long? FromStepId { get; set; }

        public PurchaseRequestStep? FromStep { get; set; }

        #endregion


        #region To Step

        /// <summary>
        /// İşlem sonrasında geçilen workflow adımı.
        /// </summary>
        public long ToStepId { get; set; }

        public PurchaseRequestStep ToStep { get; set; } = default!;

        #endregion


        #region Action

        /// <summary>
        /// Kullanıcının gerçekleştirdiği workflow aksiyonu.
        /// </summary>
        public long PurchaseRequestActionId { get; set; }

        public PurchaseRequestAction PurchaseRequestAction { get; set; } = default!;

        #endregion


        /// <summary>
        /// Kullanıcının işlem sırasında girdiği açıklama.
        ///
        /// Eski sistemdeki Description1, Description2...
        /// yaklaşımının yeni ve sade karşılığıdır.
        /// </summary>
        public string? Description { get; set; }


        #region Status

        /// <summary>
        /// İşlem öncesindeki genel talep durumu.
        /// </summary>
        public PurchaseRequestStatus PreviousStatus { get; set; }


        /// <summary>
        /// İşlem sonrasındaki genel talep durumu.
        /// </summary>
        public PurchaseRequestStatus NewStatus { get; set; }

        #endregion
    }
}