using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService
{
    public class QnbSendTechnicalServiceDto
    {
        [Required]
        public required string RequestNo { get; set; }
    }
}