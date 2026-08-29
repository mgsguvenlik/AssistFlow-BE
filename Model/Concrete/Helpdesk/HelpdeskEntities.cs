using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Helpdesk;

[Table("Ticket", Schema = "helpdesk")]
public sealed class HelpdeskTicket : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    [MaxLength(20)] public string TicketNo { get; set; } = string.Empty;
    [MaxLength(500)] public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [MaxLength(150)] public string RequesterName { get; set; } = string.Empty;
    [MaxLength(254)] public string RequesterEmail { get; set; } = string.Empty;
    public string? ToRecipients { get; set; }
    public string? CcRecipients { get; set; }
    public HelpdeskTicketStatus Status { get; set; }
    public int? Priority { get; set; }
    public bool IsSuspended { get; set; }
    public DateTimeOffset? SuspendedUntil { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
    public DateTimeOffset? AcceptanceMailSentAt { get; set; }
    public HelpdeskTicketSourceType SourceType { get; set; }
    public long? MailboxId { get; set; }
    public HelpdeskMailbox? Mailbox { get; set; }
    public ICollection<HelpdeskTicketAssignment> Assignments { get; set; } = new List<HelpdeskTicketAssignment>();
    public ICollection<HelpdeskTicketMail> Mails { get; set; } = new List<HelpdeskTicketMail>();
}

[Table("TicketAssignment", Schema = "helpdesk")]
public sealed class HelpdeskTicketAssignment : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    public long TicketId { get; set; }
    public HelpdeskTicket Ticket { get; set; } = default!;
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

[Table("TicketMail", Schema = "helpdesk")]
public sealed class HelpdeskTicketMail : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    public long TicketId { get; set; }
    public HelpdeskTicket Ticket { get; set; } = default!;
    public long? MailboxId { get; set; }
    [MaxLength(998)] public string MessageId { get; set; } = string.Empty;
    [MaxLength(998)] public string? InReplyTo { get; set; }
    public string? References { get; set; }
    public HelpdeskMailDirection Direction { get; set; }
    [MaxLength(254)] public string? FromAddress { get; set; }
    public string? ToRecipients { get; set; }
    public string? CcRecipients { get; set; }
    [MaxLength(500)] public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset MailDate { get; set; }
}

[Table("TicketComment", Schema = "helpdesk")]
public sealed class HelpdeskTicketComment : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    public long TicketId { get; set; }
    public HelpdeskTicket Ticket { get; set; } = default!;
    public string Body { get; set; } = string.Empty;
}

[Table("TicketHistory", Schema = "helpdesk")]
public sealed class HelpdeskTicketHistory : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    public long TicketId { get; set; }
    public HelpdeskTicket Ticket { get; set; } = default!;
    [MaxLength(100)] public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    [MaxLength(100)] public string? PreviousValue { get; set; }
    [MaxLength(100)] public string? NewValue { get; set; }
}

[Table("Mailbox", Schema = "helpdesk")]
public sealed class HelpdeskMailbox : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(254)] public string Address { get; set; } = string.Empty;
    [MaxLength(254)] public string Username { get; set; } = string.Empty;
    public string ProtectedPassword { get; set; } = string.Empty;
    [MaxLength(1000)] public string EwsUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

[Table("MailRule", Schema = "helpdesk")]
public sealed class HelpdeskMailRule : AuditableWithUserEntity
{
    [Key] public long Id { get; set; }
    public long MailboxId { get; set; }
    public HelpdeskMailbox Mailbox { get; set; } = default!;
    public HelpdeskRuleField Field { get; set; }
    public HelpdeskRuleOperator Operator { get; set; }
    [MaxLength(1000)] public string Value { get; set; } = string.Empty;
    public HelpdeskLogicalOperator LogicalOperator { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("TicketNumberSequence", Schema = "helpdesk")]
public sealed class HelpdeskTicketNumberSequence
{
    [Key] public int Year { get; set; }
    public int LastNumber { get; set; }
}
