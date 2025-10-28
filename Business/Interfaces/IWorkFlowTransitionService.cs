using Model.Dtos.WorkFlowDtos.WorkFlowTransition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IWorkFlowTransitionService
         : ICrudService<WorkFlowTransitionCreateDto, WorkFlowTransitionUpdateDto, WorkFlowTransitionGetDto, long>
    {
    }
}
