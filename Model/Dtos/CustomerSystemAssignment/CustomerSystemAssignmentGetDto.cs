using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerSystemAssignment
{
    public class CustomerSystemAssignmentGetDto
    {
        public long Id { get; set; }

        public long CustomerId { get; set; }

        public long CustomerSystemId { get; set; }

        public bool HasMaintenanceContract { get; set; }

        // Opsiyonel – ekranda gösterim için:
        public string? CustomerName { get; set; }          // SubscriberCompany veya ContactName1 vs.
        public string? CustomerShortCode { get; set; }     // Customer.CustomerShortCode

        public string? SystemName { get; set; }            // CustomerSystem.Name
        public string? SystemCode { get; set; }            // CustomerSystem.Code
    }
}
