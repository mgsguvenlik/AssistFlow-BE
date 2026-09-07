using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbActivityRecord
{
    public record EkbWorkFlowActivityGroupDto(
      string? RequestNo,
      int Count,
      DateTime LastOccurredAtUtc,
      IReadOnlyList<WorkFlowActivityRecorGetDto> Items
  );
}
