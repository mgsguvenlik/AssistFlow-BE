using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord
{
    public record WorkFlowActivityGroupDto(
      string? RequestNo,
      int Count,
      DateTime LastOccurredAtUtc,
      IReadOnlyList<WorkFlowActivityRecorGetDto> Items
  );
}
