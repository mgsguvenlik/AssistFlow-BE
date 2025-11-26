using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    public class WorkFlowArchive
    {
        public long Id { get; set; }

        /// <summary>Talep numarası (ana referans)</summary>
        public string RequestNo { get; set; } = default!;

        /// <summary>Arşive atılma zamanı</summary>
        public DateTime ArchivedAt { get; set; }

        /// <summary>Arşiv sebebi (Completed, Cancelled vs.)</summary>
        public string ArchiveReason { get; set; } = default!;

        /// <summary>Servis talebi ana kaydı (ServicesRequest)</summary>
        public string ServicesRequestJson { get; set; } = default!;

        /// <summary>Servis talebi ürünleri (ServicesRequestProduct[])</summary>
        public string ServicesRequestProductsJson { get; set; } = default!;

        /// <summary>Müşteri bilgileri (Customer)</summary>
        public string CustomerJson { get; set; } = default!;

        /// <summary>Teknisyen bilgileri (ApproverTechnician)</summary>
        public string ApproverTechnicianJson { get; set; } = default!;

        /// <summary>Müşteri yetkilisi bilgileri (CustomerApprover)</summary>
        public string CustomerApproverJson { get; set; } = default!;

        /// <summary>WorkFlow ana kaydı</summary>
        public string WorkFlowJson { get; set; } = default!;

        /// <summary>WorkFlowReviewLog listesi</summary>
        public string WorkFlowReviewLogsJson { get; set; } = default!;

        /// <summary>Teknik servis ana kaydı</summary>
        public string TechnicalServiceJson { get; set; } = default!;

        /// <summary>Teknik servis resimleri (Servis fotoğrafları, base64 dahil)</summary>
        public string TechnicalServiceImagesJson { get; set; } = default!;

        /// <summary>Teknik servis form resimleri (base64 dahil)</summary>
        public string TechnicalServiceFormImagesJson { get; set; } = default!;

        /// <summary>Depo kaydı</summary>
        public string WarehouseJson { get; set; } = default!;

        /// <summary>Fiyatlama kaydı</summary>
        public string PricingJson { get; set; } = default!;

        /// <summary>Son onay (FinalApproval)</summary>
        public string FinalApprovalJson { get; set; } = default!;
    }

}
