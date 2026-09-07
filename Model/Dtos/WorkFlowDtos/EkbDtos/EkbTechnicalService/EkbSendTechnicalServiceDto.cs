using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalService
{
    public class EkbSendTechnicalServiceDto
    {

        [Required]
        public required string RequestNo { get; set; }
    }
}
