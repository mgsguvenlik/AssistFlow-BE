using Core.Enums;

namespace Model.Dtos.Helpdesk;

public sealed class HelpdeskTicketCreateDto
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? ToRecipients { get; set; }
    public string? CcRecipients { get; set; }
    public int? Priority { get; set; }
    public List<long> AssignedUserIds { get; set; } = [];
}

public class HelpdeskTicketListItemDto
{
    public long Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public HelpdeskTicketSourceType SourceType { get; set; }
    public HelpdeskTicketStatus Status { get; set; }
    public int? Priority { get; set; }
    public bool IsSuspended { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public List<string> AssignedUsers { get; set; } = [];
    public List<long> AssignedUserIds { get; set; } = [];
    public int UnreadCount { get; set; }
}

public sealed class HelpdeskTicketDetailDto : HelpdeskTicketListItemDto
{
    public string Description { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? ToRecipients { get; set; }
    public string? CcRecipients { get; set; }
    public DateTimeOffset? SuspendedUntil { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
    public List<HelpdeskCommentDto> Comments { get; set; } = [];
    public List<HelpdeskHistoryDto> History { get; set; } = [];
}

public sealed class HelpdeskAssignmentDto { public List<long> UserIds { get; set; } = []; }
public sealed class HelpdeskStatusChangeDto { public HelpdeskTicketStatus Status { get; set; } public string? Description { get; set; } }
public sealed class HelpdeskPriorityChangeDto { public int? Priority { get; set; } }
public sealed class HelpdeskSuspendDto { public DateTimeOffset? SuspendedUntil { get; set; } public string? Description { get; set; } }
public sealed class HelpdeskCommentCreateDto { public string Body { get; set; } = string.Empty; }
public sealed class HelpdeskReplyDto { public string Body { get; set; } = string.Empty; }
public sealed class HelpdeskCommentDto { public long Id { get; set; } public string Body { get; set; } = string.Empty; public long CreatedUser { get; set; } public DateTimeOffset CreatedDate { get; set; } }
public sealed class HelpdeskHistoryDto { public long Id { get; set; } public string Action { get; set; } = string.Empty; public string? Description { get; set; } public string? PreviousValue { get; set; } public string? NewValue { get; set; } public long CreatedUser { get; set; } public DateTimeOffset CreatedDate { get; set; } }
public sealed class HelpdeskTicketMailDto
{
    public long Id { get; set; }
    public HelpdeskMailDirection Direction { get; set; }
    public string? FromAddress { get; set; }
    public string? SenderName { get; set; }
    public string? ToRecipients { get; set; }
    public string? CcRecipients { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset MailDate { get; set; }
}
