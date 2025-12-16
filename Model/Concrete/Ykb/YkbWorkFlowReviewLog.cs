using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbWorkFlowReviewLog", Schema = "ykb")]
    public class YkbWorkFlowReviewLog
    {
        public long Id { get; set; }

        // İş akışı / talep bağlamı
        public long YkbWorkFlowId { get; set; }
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
