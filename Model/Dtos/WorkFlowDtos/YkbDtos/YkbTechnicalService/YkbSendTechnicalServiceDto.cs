using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService
{
    public class YkbSendTechnicalServiceDto
    {

        [Required]
        public required string RequestNo { get; set; }
    }
}
