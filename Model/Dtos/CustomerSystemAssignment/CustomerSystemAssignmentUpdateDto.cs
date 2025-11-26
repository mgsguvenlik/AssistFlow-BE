using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerSystemAssignment
{
    public class CustomerSystemAssignmentUpdateDto
    {
        public long Id { get; set; }

        public long CustomerId { get; set; }

        public long CustomerSystemId { get; set; }

        /// <summary>
        /// Bakım anlaşması var mı?
        /// </summary>
        public bool HasMaintenanceContract { get; set; }
    }
}
