using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbWorkflowAttachment", Schema = "qnb")]
    public class QnbWorkflowAttachment : BaseEntity
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
        public string ContentType { get; set; } =
            "application/octet-stream";

        public long SizeBytes { get; set; }

        [Required]
        [MaxLength(10)]
        public string UploadedStepCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string LastUpdatedStepCode { get; set; } = string.Empty;
    }
}