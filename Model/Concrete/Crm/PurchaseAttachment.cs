using Core.Enums.Crm;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Crm
{
    /// <summary>
    /// CRM Satın Alma Talebine eklenen dosyaların metadata kayıtları.
    ///
    /// Dosyanın fiziksel içeriği CDN üzerinde tutulur.
    /// Bu tablo yalnızca dosya bilgilerini ve PurchaseRequest ilişkisini tutar.
    /// </summary>
    [Table("PurchaseAttachment", Schema = "crm")]
    public class PurchaseAttachment : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }


        #region Purchase Request

        /// <summary>
        /// Dosyanın bağlı olduğu satın alma talebi.
        /// </summary>
        public long PurchaseRequestId { get; set; }

        public PurchaseRequest PurchaseRequest { get; set; } = default!;

        #endregion


        #region Attachment Type

        /// <summary>
        /// Dosyanın hangi bölümde gösterileceğini belirler.
        ///
        /// General  : Dosyalar
        /// Purchase : Satınalma Dosyaları
        /// </summary>
        public PurchaseAttachmentType AttachmentType { get; set; }

        #endregion


        #region File Information

        /// <summary>
        /// Kullanıcının yüklediği orijinal dosya adı.
        /// Örn: fiyat_teklifi_abc.xlsx
        /// </summary>
        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;


        /// <summary>
        /// CDN üzerinde kullanılan fiziksel/benzersiz dosya adı veya storage key.
        /// </summary>
        [Required]
        [MaxLength(260)]
        public string StoredFileName { get; set; } = string.Empty;


        /// <summary>
        /// Dosya uzantısı.
        /// Örn: .pdf, .xlsx, .docx, .jpg
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Extension { get; set; } = string.Empty;


        /// <summary>
        /// MIME ContentType bilgisi.
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string ContentType { get; set; } = "application/octet-stream";


        /// <summary>
        /// Dosya boyutu.
        /// </summary>
        public long SizeBytes { get; set; }

        #endregion


        #region Workflow Step

        /// <summary>
        /// Dosyanın satın alma sürecinde hangi adımdayken eklendiği.
        ///
        /// Taslak aşamasında veya workflow başlamadan eklenen
        /// dosyalarda null olabilir.
        /// </summary>
        public long? UploadedStepId { get; set; }

        public PurchaseRequestStep? UploadedStep { get; set; }

        #endregion
    }
}