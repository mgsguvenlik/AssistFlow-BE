using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbAccountingProcess", Schema = "ykb")]
    public class YkbAccountingProcess : AuditableWithUserEntity
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>
        /// Muhasebe sistemine işlendi mi?
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Muhasebeye işlendiği tarih.
        /// IsProcessed = false olduğunda null olur.
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Muhasebe işlemini yapan kullanıcı.
        /// </summary>
        public long? ProcessedBy { get; set; }
    
    }
}