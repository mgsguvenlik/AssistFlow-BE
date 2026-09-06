using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    [Table("EkbWorkFlowArchive", Schema = "ekb")]
    public class EkbWorkFlowArchive
    {
        public long Id { get; set; }

        /// <summary>Talep numarası (ana referans)</summary>
        public string RequestNo { get; set; } = default!;

        /// <summary>Arşive atılma zamanı</summary>
        public DateTime ArchivedAt { get; set; }

        /// <summary>Arşiv sebebi (Completed, Cancelled vs.)</summary>
        public string ArchiveReason { get; set; } = default!;

        /// <summary>Servis talebi ana kaydı (ServicesRequest)</summary>
        public string EkbServicesRequestJson { get; set; } = default!;

        /// <summary>Servis talebi ürünleri (ServicesRequestProduct[])</summary>
        public string EkbServicesRequestProductsJson { get; set; } = default!;

        /// <summary>Müşteri bilgileri (Customer)</summary>
        public string CustomerJson { get; set; } = default!;

        /// <summary>Teknisyen bilgileri (ApproverTechnician)</summary>
        public string ApproverTechnicianJson { get; set; } = default!;

        /// <summary>Müşteri yetkilisi bilgileri (CustomerApprover)</summary>
        public string CustomerApproverJson { get; set; } = default!;

        /// <summary>WorkFlow ana kaydı</summary>
        public string EkbWorkFlowJson { get; set; } = default!;

        /// <summary>WorkFlowReviewLog listesi</summary>
        public string EkbWorkFlowReviewLogsJson { get; set; } = default!;

        /// <summary>Teknik servis ana kaydı</summary>
        public string EkbTechnicalServiceJson { get; set; } = default!;

        /// <summary>Teknik servis resimleri (Servis fotoğrafları, base64 dahil)</summary>
        public string EkbTechnicalServiceImagesJson { get; set; } = default!;

        /// <summary>Teknik servis form resimleri (base64 dahil)</summary>
        public string EkbTechnicalServiceFormImagesJson { get; set; } = default!;

        /// <summary>Depo kaydı</summary>
        public string EkbWarehouseJson { get; set; } = default!;

        /// <summary>Fiyatlama kaydı</summary>
        public string EkbPricingJson { get; set; } = default!;

        /// <summary>Son onay (FinalApproval)</summary>
        public string EkbFinalApprovalJson { get; set; } = default!;
        public string? EkbWorkflowAttachmentsJson { get; set; }
    }
}
