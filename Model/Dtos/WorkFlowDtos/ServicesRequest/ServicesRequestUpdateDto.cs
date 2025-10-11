using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.ServicesRequest
{
    public class ServicesRequestUpdateDto
    {
        [Required]
        public long Id { get; set; }

        [MaxLength(100)]
        public string? RequestNo { get; set; }

        [MaxLength(100)]
        public string? OracleNo { get; set; }

        public DateTimeOffset? ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }

        public ServicesCostStatus? ServicesCostStatus { get; set; }

        public string? Description { get; set; }

        /// <summary>Ürün/Parça ihtiyacı var mı?</summary>
        public bool IsProductRequirement { get; set; }  // <-- nullable; kısmi update

        public string? ProductList { get; set; }

        public bool? IsSended { get; set; }
        public long? SendedStatusId { get; set; }
        public bool? IsReview { get; set; }
        public bool? IsMailSended { get; set; }

        public long? CustomerApproverId { get; set; }
        public long? CustomerId { get; set; }
        public long? ServiceTypeId { get; set; }

        public List<long>? ProductIds { get; set; }
    }

}
