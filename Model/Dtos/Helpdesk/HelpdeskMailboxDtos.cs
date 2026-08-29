using Core.Enums;

namespace Model.Dtos.Helpdesk;

public sealed class HelpdeskMailboxCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string EwsUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class HelpdeskMailboxUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string EwsUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class HelpdeskMailboxDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EwsUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool HasPassword { get; set; }
}

public class HelpdeskMailRuleCreateDto
{
    public long MailboxId { get; set; }
    public HelpdeskRuleField Field { get; set; }
    public HelpdeskRuleOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
    public HelpdeskLogicalOperator LogicalOperator { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class HelpdeskMailRuleUpdateDto : HelpdeskMailRuleCreateDto { }

public sealed class HelpdeskMailRuleDto : HelpdeskMailRuleCreateDto
{
    public long Id { get; set; }
}
