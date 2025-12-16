using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbActivityRecord
{
    public record YkbWorkFlowActivityGroupDto(
      string? RequestNo,
      int Count,
      DateTime LastOccurredAtUtc,
      IReadOnlyList<WorkFlowActivityRecorGetDto> Items
  );
}
