namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public sealed class FinishWorkingResultDto
    {
        public string RequestNo { get; set; } = string.Empty;

        public int SerialNo { get; set; }

        public bool IsFinished { get; set; }

        public bool NeedConfirmation { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<string> ReceivedZones { get; set; } = new();

        public List<string> MissingZones { get; set; } = new();
    }
}