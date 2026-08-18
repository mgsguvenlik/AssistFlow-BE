using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseRequestTask
{
    public class PurchaseRequestTaskGetDto
    {
        public long Id { get; set; }

        public long PurchaseRequestId { get; set; }

        public long PurchaseRequestStepId { get; set; }

        public string? StepCode { get; set; }

        public string? StepName { get; set; }


        // Assignment
        public long? AssignedUserId { get; set; }

        public string? AssignedUserName { get; set; }

        public long? AssignedRoleId { get; set; }

        public string? AssignedRoleName { get; set; }


        // Status
        public PurchaseRequestTaskStatus Status { get; set; }

        public string? StatusName { get; set; }


        // Completion
        public DateTimeOffset? CompletedDate { get; set; }

        public long? CompletedUserId { get; set; }

        public string? CompletedUserName { get; set; }


        public DateTimeOffset CreatedDate { get; set; }
    }
}