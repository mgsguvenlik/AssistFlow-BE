using Model.Abstractions;

namespace Model.Concrete.WorkFlows
{
    public class WorkFlowReviewLog : BaseEntity
    {
        public long Id { get; set; }

        // İş akışı / talep bağlamı
        public long WorkFlowId { get; set; }
        public string RequestNo { get; set; } = default!;

        // Adım bilgileri
        public long? FromStepId { get; set; }
        public string FromStepCode { get; set; } = default!;
        public long? ToStepId { get; set; }
        public string ToStepCode { get; set; } = default!;

        // Kullanıcı notu
        public string ReviewNotes { get; set; } = default!;

        // Audit alanları
        public long CreatedUser { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
