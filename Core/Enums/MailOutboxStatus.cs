using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Enums
{
    public enum MailOutboxStatus
    {
        Pending = 0,
        InProgress = 1,
        Sent = 2,
        Failed = 3
    }
}
