using Model.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IMailPushService
    {
        Task EnqueueAsync(MailOutbox item);
    }
}
