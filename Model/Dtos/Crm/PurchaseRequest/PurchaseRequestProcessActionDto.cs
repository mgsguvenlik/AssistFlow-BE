using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.PurchaseRequest 
{
    public class PurchaseRequestProcessActionDto
    {
        [Required]
        public long PurchaseRequestId { get; set; }

        [Required]
        public long PurchaseRequestActionId { get; set; }

        /// <summary>
        /// Kullanıcının işlem açıklaması.
        /// Action.RequiresDescription = true ise zorunlu kontrol edilecek.
        /// </summary>
        public string? Description { get; set; }
    }
}