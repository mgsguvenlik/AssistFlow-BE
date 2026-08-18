using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// CRM Satın Alma workflow adımlarında
    /// kullanıcıya sunulabilecek aksiyonlar.
    /// </summary>
    [Table("PurchaseRequestAction", Schema = "crm")]
    public class PurchaseRequestAction : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }


        #region Source Step

        /// <summary>
        /// Aksiyonun kullanılabileceği workflow adımı.
        /// </summary>
        public long PurchaseRequestStepId { get; set; }

        public PurchaseRequestStep PurchaseRequestStep { get; set; } = default!;

        #endregion


        /// <summary>
        /// Kod tarafında kullanılacak aksiyon kodu.
        ///
        /// Örn:
        /// SUBMIT
        /// APPROVE
        /// REJECT
        /// RESEARCH_COMPLETED
        /// PROCUREMENT_COMPLETED
        /// </summary>
        public string Code { get; set; } = string.Empty;


        /// <summary>
        /// Kullanıcıya gösterilecek buton / işlem adı.
        ///
        /// Örn:
        /// Uygundur
        /// Red
        /// Araştırma Yapıldı
        /// Tedarik Edildi
        /// </summary>
        public string Name { get; set; } = string.Empty;


        /// <summary>
        /// Aksiyon açıklaması.
        /// </summary>
        public string? Description { get; set; }


        #region Target Step

        /// <summary>
        /// Aksiyonun varsayılan hedef workflow adımı.
        ///
        /// Koşullu workflow geçişlerinde null olabilir.
        ///
        /// Örneğin Tedarik Edildi işleminden sonra:
        /// fiziksel ürün varsa WarehouseControl,
        /// yoksa Accounting seçilebilir.
        /// </summary>
        public long? TargetStepId { get; set; }

        public PurchaseRequestStep? TargetStep { get; set; }

        #endregion


        /// <summary>
        /// Bu işlem sırasında kullanıcının açıklama
        /// girmesi zorunlu mu?
        ///
        /// Özellikle Red / Revizyon işlemlerinde kullanılacaktır.
        /// </summary>
        public bool RequiresDescription { get; set; }


        /// <summary>
        /// Kullanıcı arayüzündeki aksiyon sırası.
        /// </summary>
        public int OrderNo { get; set; }


        /// <summary>
        /// Aksiyon aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}