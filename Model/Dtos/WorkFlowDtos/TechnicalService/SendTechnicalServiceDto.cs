using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class SendTechnicalServiceDto
    {

        [Required]
        public required string RequestNo { get; set; }
    }
}
