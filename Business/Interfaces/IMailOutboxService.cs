using Model.Dtos.MailOutbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IMailOutboxService
    : ICrudService<MailOutboxCreateDto, MailOutboxUpdateDto, MailOutboxGetDto, long>
    {
        Task<bool> RetryAsync(long id, CancellationToken ct = default);
    }
}
