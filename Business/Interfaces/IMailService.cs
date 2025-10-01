using Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IMailService
    {
        Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to);
    }
}
