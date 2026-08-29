namespace Core.Enums;

public enum HelpdeskTicketStatus { Created = 0, Assigned = 1, InProgress = 2, Completed = 3, Reopened = 4, Rejected = 5 }
public enum HelpdeskTicketSourceType { Manual = 0, Mail = 1 }
public enum HelpdeskMailDirection { Incoming = 0, Outgoing = 1 }
public enum HelpdeskRuleField { Subject = 0, Body = 1, Sender = 2 }
public enum HelpdeskRuleOperator { Contains = 0, Equals = 1 }
public enum HelpdeskLogicalOperator { And = 0, Or = 1 }
