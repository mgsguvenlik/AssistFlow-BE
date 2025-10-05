using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Customer
{
    public class CustomerGetDto
    {
        public long Id { get; set; }
        public string? SubscriberCode { get; set; }
        public string? SubscriberCompany { get; set; }
        public string? CustomerMainGroupName { get; set; }
        public string? SubscriberAddress { get; set; }
        public string? City { get; set; }
        public string? LocationCode { get; set; }
        public string? OracleCode { get; set; }
        public string? ContactName1 { get; set; }
        public string? Phone1 { get; set; }
        public string? Email1 { get; set; }
        public string? ContactName2 { get; set; }
        public string? Phone2 { get; set; }
        public string? Email2 { get; set; }
        public string? CustomerShortCode { get; set; }
        public string? CorporateLocationId { get; set; }
        public long? CustomerTypeId { get; set; }
    }
}
