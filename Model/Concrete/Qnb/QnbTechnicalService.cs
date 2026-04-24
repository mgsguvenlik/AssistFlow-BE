using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbTechnicalService", Schema = "qnb")]
    public class QnbTechnicalService : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        public long? ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public bool IsLocationCheckRequired { get; set; } = true;

        public TechnicalServiceStatus ServicesStatus { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }

        public ICollection<QnbTechnicalServiceImage> QnbServicesImages { get; set; } = new List<QnbTechnicalServiceImage>();
        public ICollection<QnbTechnicalServiceFormImage> QnbServiceRequestFormImages { get; set; } = new List<QnbTechnicalServiceFormImage>();
    }
}