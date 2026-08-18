using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseRequest
{
    public class PurchaseRequestGetDto
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = string.Empty;


        // Tenant
        public long? TenantId { get; set; }
        public string? TenantName { get; set; }


        // Requester
        public long RequesterUserId { get; set; }
        public string? RequesterUserName { get; set; }


        // Manager
        public long? ManagerUserId { get; set; }
        public string? ManagerUserName { get; set; }


        // Request
        public string Subject { get; set; } = string.Empty;

        public string? Description { get; set; }

        public PurchaseRequestType RequestType { get; set; }

        public string? RequestTypeName { get; set; }

        public bool IsOfficePurchase { get; set; }


        // Customer
        public long? CustomerId { get; set; }

        public string? CustomerName { get; set; }


        // System Type
        public long? SystemTypeId { get; set; }

        public string? SystemTypeName { get; set; }


        // Status
        public PurchaseRequestStatus Status { get; set; }

        public string? StatusName { get; set; }


        // Current Step
        public long? CurrentStepId { get; set; }

        public string? CurrentStepCode { get; set; }

        public string? CurrentStepName { get; set; }


        public DateTimeOffset? ClosedDate { get; set; }


        // Audit
        public DateTimeOffset CreatedDate { get; set; }

        public DateTimeOffset? UpdatedDate { get; set; }

        public long CreatedUser { get; set; }

        public long? UpdatedUser { get; set; }
    }
}