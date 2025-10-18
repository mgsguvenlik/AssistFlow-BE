using Core.Enums;
using Model.Dtos.WorkFlowDtos.UsedMaterial;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class TechnicalServiceCreateDto
    {
        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;
        public long? ServiceTypeId { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public string? ProblemDescription { get; set; } //Problem Tanımı
        public string? ResolutionAndActions { get; set; }//Çözüm ve İşlemler
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StartLocation { get; set; }//Örn: "41.01224, 28.976018"
        public string? EndLocation { get; set; }//Örn: "41.01224, 28.976018"
        public TechnicalServiceStatus ServicesStatus { get; set; } = TechnicalServiceStatus.Pending;
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public List<string>? ServiceImageUrls { get; set; }
        public List<string>? FormImageUrls { get; set; }
        public List<UsedMaterialCreateDto>? UsedMaterials { get; set; }
    }
}
