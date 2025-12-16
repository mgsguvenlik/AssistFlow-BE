using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class StartTechnicalServiceDto
    {
        [Required]
        public required string RequestNo { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? StartLocation { get; set; }//Örn: "41.01224, 28.976018"
        //public List<ServicesRequestProductCreateDto>? Products { get; set; }
    }
}
