using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbServicesRequestServiceTypes", Schema = "qnb")]
    public class QnbServicesRequestServiceType
    {
        public long QnbServicesRequestId { get; set; }
        public QnbServicesRequest QnbServicesRequest { get; set; } = default!;
        public long ServiceTypeId { get; set; }
        public ServiceType ServiceType { get; set; } = default!;
    }
}
