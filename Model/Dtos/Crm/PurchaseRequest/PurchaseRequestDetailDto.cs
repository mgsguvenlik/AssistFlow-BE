using Model.Dtos.Crm.PurchaseAttachment;
using Model.Dtos.Crm.PurchaseRequestAction;
using Model.Dtos.Crm.PurchaseRequestHistory;
using Model.Dtos.Crm.PurchaseRequestItem;
using Model.Dtos.Crm.PurchaseRequestTask;

namespace Model.Dtos.Crm.PurchaseRequest
{
    public class PurchaseRequestDetailDto : PurchaseRequestGetDto
    {
        public List<PurchaseRequestItemGetDto> Items { get; set; }
            = new();

        public List<PurchaseRequestTaskGetDto> Tasks { get; set; }
            = new();

        public List<PurchaseRequestHistoryGetDto> Histories { get; set; }
            = new();

        public List<PurchaseAttachmentGetDto> Attachments { get; set; }
            = new();

        /// <summary>
        /// Mevcut kullanıcı ve mevcut step için
        /// yapılabilecek aksiyonlar.
        /// </summary>
        public List<PurchaseRequestActionGetDto> AvailableActions { get; set; }
            = new();
    }
}