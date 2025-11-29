using Core.Enums;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequest
{
    public class YkbServicesRequestUpdateDto
    {
        public long Id { get; set; }
        public string? OracleNo { get; set; }
        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }
        public long? WorkFlowStepId { get; set; }
        public long CustomerId { get; set; }
        public long ServiceTypeId { get; set; }
        public long? CustomerApproverId { get; set; }
        public WorkFlowPriority Priority { get; set; }
        public ServicesRequestStatus ServicesRequestStatus { get; set; }
        public bool IsMailSended { get; set; }
        [MaxLength(100)]
        [Required]
        public required string RequestNo { get; set; }
        public bool IsLocationValid { get; set; }
        public string? CustomerApproverName { get; set; }
        public long? ApproverTechnicianId { get; set; }
        public string? ApproverTechnician { get; set; }
        public List<YkbServicesRequestProductUpdateDto>? Products { get; set; }
        public long StatuId { get; set; } //Akış taleplerin id si. Ekranda seçilecek.
    }
}
