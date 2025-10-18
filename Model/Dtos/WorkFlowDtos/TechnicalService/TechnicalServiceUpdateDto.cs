using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.UsedMaterial;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class TechnicalServiceUpdateDto
    {
        [Required]
        public long Id { get; set; }
        public string? RequestNo { get; set; }
        public long? ServiceTypeId { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StartLocation { get; set; }//Örn: "41.01224, 28.976018"
        public string? EndLocation { get; set; }//Örn: "41.01224, 28.976018"
        public TechnicalServiceStatus? ServicesStatus { get; set; }
        public ServicesCostStatus? ServicesCostStatus { get; set; }
        public List<string>? ServiceImageUrls { get; set; }
        public List<string>? FormImageUrls { get; set; }
        public List<UsedMaterialUpdateDto>? UsedMaterials { get; set; }
        public List<ServicesRequestProductCreateDto>? Products { get; set; }
    }
}
