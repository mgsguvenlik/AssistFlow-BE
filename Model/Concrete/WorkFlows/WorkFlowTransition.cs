using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    // Bir WorkFlowStep'ten diğerine geçiş kuralını tanımlar
    public class WorkFlowTransition : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        // Geçişin başladığı adım (From)
        [ForeignKey(nameof(FromStep))]
        public long FromStepId { get; set; }
        public WorkFlowStep FromStep { get; set; } = default!;

        // Geçişin bittiği adım (To)
        [ForeignKey(nameof(ToStep))]
        public long ToStepId { get; set; }
        public WorkFlowStep ToStep { get; set; } = default!;

        [Required, MaxLength(100)]
        public string TransitionName { get; set; } = string.Empty; // Örn: "Onayla ve Depoya Gönder", "Revize Et ve Talebe Dön"

        // Geçiş için ek koşullar (Örn: Role bağlı, Duruma bağlı, vb. - opsiyonel)
        public string? Condition { get; set; }
    }
}
