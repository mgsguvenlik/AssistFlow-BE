using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.WorkFlows
{
    public class WorkFlowStep : BaseEntity 
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }
        public int Order { get; set; } // Adımın sırası (1, 2, 3...)

        // Bu adımdan çıkan geçişler (örneğin "Servis Talebi → Depo Teslimatı")
        public ICollection<WorkFlowTransition> OutgoingTransitions { get; set; } = new List<WorkFlowTransition>();

        // Bu adıma gelen geçişler (örneğin "Depo Teslimatı → Teknik Servis")
        public ICollection<WorkFlowTransition> IncomingTransitions { get; set; } = new List<WorkFlowTransition>();
    }
}
