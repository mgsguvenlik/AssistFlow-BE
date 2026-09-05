using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbServicesRequestServiceTypes", Schema = "ykb")]
    public class YkbServicesRequestServiceType
    {
        public long YkbServicesRequestId { get; set; }
        public YkbServicesRequest YkbServicesRequest { get; set; } = default!;
        public long ServiceTypeId { get; set; }
        public ServiceType ServiceType { get; set; } = default!;
    }
}
