using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    [Table("EkbWorkflowAttachment", Schema = "ekb")]
    public class EkbWorkflowAttachment : BaseEntity
    {

        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string RequestNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(260)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Extension { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        /// <summary>
        /// Dosyanın ilk eklendiği adım: PRC veya APR.
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string UploadedStepCode { get; set; } = string.Empty;

        /// <summary>
        /// Dosyanın en son güncellendiği adım.
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string LastUpdatedStepCode { get; set; } = string.Empty;
    }
}