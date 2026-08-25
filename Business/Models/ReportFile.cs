using Core.Enums;

namespace Business.Models
{
    public sealed record ReportFile(
        string FileName,
        string ContentType,
        byte[] Content);

    public sealed record MailAttachmentData(
        string FileName,
        string ContentType,
        ReadOnlyMemory<byte> Content);

    public sealed class ReportData
    {
        public List<string> Columns { get; init; } = new();
        public List<Dictionary<string, object?>> Rows { get; init; } = new();
        public bool IsTruncated { get; init; }
    }

    public sealed record SqlValidationResult(bool IsValid, IReadOnlyList<string> Errors)
    {
        public static SqlValidationResult Success { get; } = new(true, Array.Empty<string>());
    }

    public sealed record ReportExecutionOutcome(
        bool Acquired,
        long? ExecutionId,
        PeriodicReportExecutionStatus? Status,
        string? Message);
}
