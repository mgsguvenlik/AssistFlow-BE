using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class YkbDashboardKpiDto
    {
        public int TotalWorkFlows { get; set; }
        public int CompletedWorkFlows { get; set; }
        public int NotCompletedWorkFlows { get; set; }

        public int InServiceRequest { get; set; }      // SR
        public int InWarehouse { get; set; }           // WH
        public int InTechnicalService { get; set; }    // TS
        public int InPricing { get; set; }             // PRC
        public int InFinalApproval { get; set; }       // APR
        public int InCustomerApproval { get; set; }    // CAPR
    }
}
