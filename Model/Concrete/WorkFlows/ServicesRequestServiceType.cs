using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    [Table("ServicesRequestServiceTypes", Schema = "dbo")]
    public class ServicesRequestServiceType
    {
        public long ServicesRequestId { get; set; }
        public ServicesRequest ServicesRequest { get; set; } = default!;
        public long ServiceTypeId { get; set; }
        public ServiceType ServiceType { get; set; } = default!;
    }
}
