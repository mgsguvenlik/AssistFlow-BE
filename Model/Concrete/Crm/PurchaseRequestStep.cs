using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// CRM Satın Alma workflow adımları.
    /// </summary>
    [Table("PurchaseRequestStep", Schema = "crm")]
    public class PurchaseRequestStep : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }


        /// <summary>
        /// Kod tarafında kullanılacak sabit adım kodu.
        ///
        /// Örn:
        /// DRAFT
        /// MANAGER_FIRST_APPROVAL
        /// PURCHASING_RESEARCH
        /// PROCUREMENT
        /// </summary>
        public string Code { get; set; } = string.Empty;


        /// <summary>
        /// Kullanıcıya gösterilecek adım adı.
        /// </summary>
        public string Name { get; set; } = string.Empty;


        /// <summary>
        /// Adımın açıklaması.
        /// </summary>
        public string? Description { get; set; }


        /// <summary>
        /// Ekran ve süreç şeması üzerindeki genel sıralama.
        ///
        /// Workflow geçişleri bu alandan belirlenmez.
        /// </summary>
        public int OrderNo { get; set; }


        /// <summary>
        /// Sürecin başlangıç adımı mı?
        /// </summary>
        public bool IsInitial { get; set; }


        /// <summary>
        /// Sürecin terminal/son adımı mı?
        ///
        /// Completed, Rejected, Cancelled gibi.
        /// </summary>
        public bool IsFinal { get; set; }


        /// <summary>
        /// Kullanılabilir workflow adımı mı?
        /// </summary>
        public bool IsActive { get; set; } = true;


        #region Navigations

        /// <summary>
        /// Bu adımda kullanıcıya sunulabilecek aksiyonlar.
        /// </summary>
        public ICollection<PurchaseRequestAction> Actions { get; set; }
            = new List<PurchaseRequestAction>();

        #endregion
    }
}