using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class FinishTechnicalServiceDto
    {
      
        [Required]
        public required string RequestNo { get; set; }
        public long? ServiceTypeId { get; set; }
        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? EndLocation { get; set; }//Örn: "41.01224, 28.976018"
        public ServicesCostStatus ServicesCostStatus { get; set; }

        [Required(ErrorMessage = "En az bir servis fotoğrafı yüklemelisiniz.")]
        [MinLength(1, ErrorMessage = "En az bir servis fotoğrafı seçin.")]
        public List<IFormFile>? ServiceImages { get; set; }

        [Required(ErrorMessage = "En az bir form görseli yüklemelisiniz.")]
        [MinLength(1, ErrorMessage = "En az bir form görseli seçin.")]
        public List<IFormFile>? FormImages { get; set; }
        public List<ServicesRequestProductCreateDto>? Products { get; set; }
    }
}
