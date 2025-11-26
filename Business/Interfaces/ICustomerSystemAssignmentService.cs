using Model.Dtos.CustomerSystemAssignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface ICustomerSystemAssignmentService
          : ICrudService<CustomerSystemAssignmentCreateDto, CustomerSystemAssignmentUpdateDto, CustomerSystemAssignmentGetDto, long>
    {
        Task<List<CustomerSystemAssignmentGetDto>> GetByCustomerIdAsync(long customerId);
    }
}
