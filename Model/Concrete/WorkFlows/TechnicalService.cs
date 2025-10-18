using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.WorkFlows
{
    public class TechnicalService : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        // Hizmet türü (lookup tabloyu FK ile bağlıyorum — projende ServicesType tablosu varsa ona uyar)
        public long? ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        // Gerçekleşen zaman damgaları
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        // Teknik alanlar
        public string? ProblemDescription { get; set; } //Problem Tanımı
        public string? ResolutionAndActions { get; set; }//Alınan Aksiyonlar ve Çözüm
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? StartLocation { get; set; }//Örn: "41.01224, 28.976018"
        public string? EndLocation { get; set; }//Örn: "41.01224, 28.976018"

        // Durumlar
        public TechnicalServiceStatus ServicesStatus { get; set; } = TechnicalServiceStatus.Pending;
        public ServicesCostStatus ServicesCostStatus { get; set; }

        // Görseller
        public ICollection<TechnicalServiceImage> ServicesImages { get; set; } = new List<TechnicalServiceImage>();
        public ICollection<TechnicalServiceFormImage> ServiceRequestFormImages { get; set; } = new List<TechnicalServiceFormImage>();
        public ICollection<UsedMaterial> UsedMaterials { get; set; } = new List<UsedMaterial>();
    }
}
