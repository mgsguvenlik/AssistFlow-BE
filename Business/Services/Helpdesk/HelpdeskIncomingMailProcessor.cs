using Business.Interfaces.Helpdesk;
using Core.Enums;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.Helpdesk;
using System.Net;
using System.Text.RegularExpressions;

namespace Business.Services.Helpdesk;

public sealed partial class HelpdeskIncomingMailProcessor(
    AppDataContext db,
    IHelpdeskTicketNumberGenerator numberGenerator,
    ILogger<HelpdeskIncomingMailProcessor> logger,
    IOptionsSnapshot<AppSettings> appSettings) : IHelpdeskIncomingMailProcessor
{
    private static readonly string[] ManagerRoleCodes = ["HELPDESK_MANAGER", "HELPDESK_TEAM_LEAD"];
    private readonly AppDataContext _db = db;
    private readonly IHelpdeskTicketNumberGenerator _numberGenerator = numberGenerator;
    private readonly ILogger<HelpdeskIncomingMailProcessor> _logger = logger;
    private readonly IOptionsSnapshot<AppSettings> _appSettings = appSettings;

    public async Task<bool> ProcessAsync(HelpdeskMailbox mailbox, HelpdeskInboundMail mail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mail.MessageId))
        {
            _logger.LogWarning("Helpdesk maili MessageId bulunamadığı için atlandı. MailboxId={MailboxId}", mailbox.Id);
            return false;
        }
        if (await _db.HelpdeskTicketMails.AnyAsync(x => x.MailboxId == mailbox.Id && x.MessageId == mail.MessageId, ct))
        {
            _logger.LogInformation("Helpdesk maili daha önce işlendiği için atlandı. MailboxId={MailboxId}", mailbox.Id);
            return true;
        }

        var existing = await FindExistingTicketAsync(mail, ct);
        if (existing is not null)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            _db.HelpdeskTicketMails.Add(ToMail(existing.Id, mailbox.Id, mail));
            AddHistory(existing.Id, "IncomingMailReceived", mail.Subject);
            await NotifyTicketUsersAsync(existing, $"{existing.TicketNo} numaralı talebe yeni mail geldi.", ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _logger.LogInformation("Helpdesk gelen maili mevcut ticket'a kaydedildi. MailboxId={MailboxId}, TicketId={TicketId}", mailbox.Id, existing.Id);
            return true;
        }

        var rules = await _db.HelpdeskMailRules.AsNoTracking()
            .Where(x => x.MailboxId == mailbox.Id && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        if (!RulesMatch(rules, mail))
        {
            _logger.LogInformation("Helpdesk gelen maili aktif kurallarla eşleşmedi ve okunmamış bırakıldı. MailboxId={MailboxId}, ActiveRuleCount={ActiveRuleCount}",
                mailbox.Id, rules.Count);
            return false;
        }

        await using var createTransaction = await _db.Database.BeginTransactionAsync(ct);
        var ticket = new HelpdeskTicket
        {
            TicketNo = await _numberGenerator.NextAsync(ct),
            Subject = string.IsNullOrWhiteSpace(mail.Subject) ? "Konusuz mail" : mail.Subject.Trim(),
            Description = mail.Body,
            RequesterName = string.IsNullOrWhiteSpace(mail.FromName) ? mail.FromAddress : mail.FromName,
            RequesterEmail = mail.FromAddress,
            ToRecipients = mail.ToRecipients,
            CcRecipients = mail.CcRecipients,
            Status = HelpdeskTicketStatus.Created,
            SourceType = HelpdeskTicketSourceType.Mail,
            MailboxId = mailbox.Id,
            CreatedDate = DateTimeOffset.Now,
            CreatedUser = 0
        };
        _db.HelpdeskTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        _db.HelpdeskTicketMails.Add(ToMail(ticket.Id, mailbox.Id, mail));
        AddHistory(ticket.Id, "CreatedFromMail", mail.Subject);
        await NotifyManagersAsync(ticket, ct);
        await _db.SaveChangesAsync(ct);
        await createTransaction.CommitAsync(ct);
        _logger.LogInformation("Helpdesk gelen mailinden ticket oluşturuldu. MailboxId={MailboxId}, TicketId={TicketId}", mailbox.Id, ticket.Id);
        return true;
    }

    private async Task<HelpdeskTicket?> FindExistingTicketAsync(HelpdeskInboundMail mail, CancellationToken ct)
    {
        var replyMessageIds = ExtractMessageIds(mail.InReplyTo).ToList();
        if (replyMessageIds.Count > 0)
        {
            var ticket = await _db.HelpdeskTicketMails.Where(x => replyMessageIds.Contains(x.MessageId))
                .Select(x => x.Ticket).FirstOrDefaultAsync(x => !x.IsDeleted, ct);
            if (ticket is not null) return ticket;
        }

        var references = ExtractMessageIds(mail.References).ToList();
        if (references.Count > 0)
        {
            var ticket = await _db.HelpdeskTicketMails.Where(x => references.Contains(x.MessageId))
                .OrderByDescending(x => x.MailDate).Select(x => x.Ticket).FirstOrDefaultAsync(x => !x.IsDeleted, ct);
            if (ticket is not null) return ticket;
        }

        var match = TicketNoRegex().Match(mail.Subject ?? string.Empty);
        if (match.Success)
        {
            var ticketNo = match.Groups[1].Value;
            return await _db.HelpdeskTickets.FirstOrDefaultAsync(x => x.TicketNo == ticketNo && !x.IsDeleted, ct);
        }

        if (ReplySubjectRegex().IsMatch(mail.Subject ?? string.Empty) && !string.IsNullOrWhiteSpace(mail.FromAddress))
        {
            var normalizedSubject = NormalizeReplySubject(mail.Subject);
            var candidates = await _db.HelpdeskTickets.AsNoTracking()
                .Where(x => !x.IsDeleted && x.RequesterEmail == mail.FromAddress)
                .OrderByDescending(x => x.CreatedDate)
                .Take(50)
                .ToListAsync(ct);
            var ticket = candidates.FirstOrDefault(x => string.Equals(
                NormalizeReplySubject(x.Subject), normalizedSubject, StringComparison.OrdinalIgnoreCase));
            if (ticket is not null) return ticket;
        }
        return null;
    }

    private static bool RulesMatch(IReadOnlyList<HelpdeskMailRule> rules, HelpdeskInboundMail mail)
    {
        if (rules.Count == 0) return false;
        var result = Match(rules[0], mail);
        for (var index = 1; index < rules.Count; index++)
            result = rules[index].LogicalOperator == HelpdeskLogicalOperator.And
                ? result && Match(rules[index], mail)
                : result || Match(rules[index], mail);
        return result;
    }

    private static bool Match(HelpdeskMailRule rule, HelpdeskInboundMail mail)
    {
        var source = rule.Field switch
        {
            HelpdeskRuleField.Subject => mail.Subject,
            HelpdeskRuleField.Body => mail.Body,
            HelpdeskRuleField.Sender => mail.FromAddress,
            _ => string.Empty
        };
        return rule.Operator == HelpdeskRuleOperator.Equals
            ? string.Equals(source?.Trim(), rule.Value.Trim(), StringComparison.OrdinalIgnoreCase)
            : source?.Contains(rule.Value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task NotifyManagersAsync(HelpdeskTicket ticket, CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted &&
                x.UserRoles.Any(ur => ur.Role != null && ur.Role.Code != null && ManagerRoleCodes.Contains(ur.Role.Code)))
            .Select(x => new { x.Id, x.Email }).Distinct().ToListAsync(ct);
        foreach (var user in users)
            _db.Notifications.Add(NewNotification(user.Id, ticket.TicketNo, "Yeni Helpdesk talebi", $"{ticket.TicketNo} numaralı yeni talep oluşturuldu."));
        var recipients = string.Join(';', users.Select(x => x.Email).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(recipients))
        {
            var detailUrl = $"{_appSettings.Value.FrontUrl.TrimEnd('/')}/helpdesk/tickets/{ticket.Id}";
            _db.MailOutboxes.Add(NewOutbox(ticket.TicketNo, recipients, $"Yeni Helpdesk Talebi Oluşturuldu - {ticket.TicketNo}", BuildManagerNotificationMailBody(ticket, detailUrl)));
        }
    }

    private static string BuildManagerNotificationMailBody(HelpdeskTicket ticket, string detailUrl)
    {
        var ticketNo = WebUtility.HtmlEncode(ticket.TicketNo);
        var subject = WebUtility.HtmlEncode(ticket.Subject);
        var requesterName = WebUtility.HtmlEncode(ticket.RequesterName);
        var requesterEmail = WebUtility.HtmlEncode(ticket.RequesterEmail);
        var createdDate = ticket.CreatedDate.ToString("dd.MM.yyyy HH:mm");
        var safeDetailUrl = WebUtility.HtmlEncode(detailUrl);

        return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8' /></head>
<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8;padding:24px 0;'>
        <tr><td align='center'>
            <table width='640' cellpadding='0' cellspacing='0' style='width:100%;max-width:640px;background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;'>
                <tr><td style='background-color:#1f4e79;color:#ffffff;padding:20px 24px;'><h2 style='margin:0;font-size:20px;'>Yeni Helpdesk Talebi Oluşturuldu</h2></td></tr>
                <tr><td style='padding:24px;'>
                    <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>Merhaba,</p>
                    <p style='font-size:15px;line-height:1.6;margin:0 0 20px;'><strong>{ticketNo}</strong> numaralı yeni Helpdesk talebi e-posta üzerinden otomatik olarak oluşturuldu. Talep henüz bir personele atanmamıştır.</p>
                    <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin-top:12px;'>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;width:180px;'><strong>Talep No</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{ticketNo}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Başlık</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{subject}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Talep Eden</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{requesterName}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>E-posta</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'><a href='mailto:{requesterEmail}' style='color:#2563eb;'>{requesterEmail}</a></td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Oluşturma Tarihi</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{createdDate}</td></tr>
                    </table>
                    <p style='font-size:14px;line-height:1.6;margin:22px 0 14px;color:#6b7280;'>Talebi incelemek ve uygun personele atamak için aşağıdaki bağlantıyı kullanabilirsiniz.</p>
                    <a href='{safeDetailUrl}' style='display:inline-block;padding:11px 20px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;font-size:14px;font-weight:600;'>Talebi Görüntüle</a>
                    <p style='margin:14px 0 0;font-size:12px;word-break:break-all;'><a href='{safeDetailUrl}' style='color:#2563eb;'>{safeDetailUrl}</a></p>
                    <p style='font-size:13px;line-height:1.6;margin:24px 0 0;color:#6b7280;'>Bu bildirim FlowAssist Helpdesk sistemi tarafından otomatik olarak oluşturulmuştur.</p>
                </td></tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
    }

    private async Task NotifyTicketUsersAsync(HelpdeskTicket ticket, string message, CancellationToken ct)
    {
        var users = await _db.HelpdeskTicketAssignments.AsNoTracking().Where(x => x.TicketId == ticket.Id && x.IsActive)
            .Select(x => new { x.UserId, x.User.Email }).ToListAsync(ct);
        if (users.Count == 0)
        {
            var managerUsers = await _db.Users.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted &&
                    x.UserRoles.Any(ur => ur.Role != null && ur.Role.Code != null && ManagerRoleCodes.Contains(ur.Role.Code)))
                .Select(x => new { UserId = x.Id, x.Email }).Distinct().ToListAsync(ct);
            users.AddRange(managerUsers);
        }
        foreach (var user in users.DistinctBy(x => x.UserId))
            _db.Notifications.Add(NewNotification(user.UserId, ticket.TicketNo, "Helpdesk yeni mail", message));
        var recipients = string.Join(';', users.Select(x => x.Email).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(recipients))
            _db.MailOutboxes.Add(NewOutbox(ticket.TicketNo, recipients, $"[{ticket.TicketNo}] Yeni mail", $"<p>{message}</p>"));
    }

    private static HelpdeskTicketMail ToMail(long ticketId, long mailboxId, HelpdeskInboundMail mail) => new()
    {
        TicketId = ticketId, MailboxId = mailboxId, MessageId = mail.MessageId,
        InReplyTo = NormalizeMessageId(mail.InReplyTo), References = mail.References,
        Direction = HelpdeskMailDirection.Incoming, FromAddress = mail.FromAddress,
        ToRecipients = mail.ToRecipients, CcRecipients = mail.CcRecipients, Subject = mail.Subject,
        Body = mail.Body, MailDate = mail.MailDate, CreatedDate = DateTimeOffset.Now, CreatedUser = 0
    };

    private void AddHistory(long ticketId, string action, string? description) => _db.HelpdeskTicketHistories.Add(new HelpdeskTicketHistory { TicketId = ticketId, Action = action, Description = description, CreatedDate = DateTimeOffset.Now, CreatedUser = 0 });
    private static Notification NewNotification(long userId, string ticketNo, string title, string message) => new() { Type = NotificationType.GenericInfo, Scope = NotificationScope.User, TargetUserId = userId, RequestNo = ticketNo, Title = title, Message = message, IsRead = false, CreatedDate = DateTime.Now, CreatedUser = 0 };
    private static MailOutbox NewOutbox(string ticketNo, string recipients, string subject, string body) => new() { RequestNo = ticketNo, ToRecipients = recipients, Subject = subject, BodyHtml = body, Status = MailOutboxStatus.Pending, CreatedDate = DateTime.Now, CreatedUser = 0 };
    private static string? NormalizeMessageId(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var trimmed = value.Trim(); return trimmed.StartsWith('<') ? trimmed : $"<{trimmed.Trim('<', '>')}>"; }
    private static IEnumerable<string> ExtractMessageIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        var matches = MessageIdRegex().Matches(value);
        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                var normalized = NormalizeMessageId(match.Value);
                if (normalized is not null) yield return normalized;
            }
            yield break;
        }
        foreach (var token in value.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = NormalizeMessageId(token);
            if (normalized is not null) yield return normalized;
        }
    }
    private static string NormalizeReplySubject(string? subject) => ReplySubjectRegex().Replace(subject?.Trim() ?? string.Empty, string.Empty).Trim();

    [GeneratedRegex(@"<[^<>\s]+>", RegexOptions.Compiled)]
    private static partial Regex MessageIdRegex();
    [GeneratedRegex(@"\[?(HD-\d{4}-\d{6})\]?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TicketNoRegex();
    [GeneratedRegex(@"^(?:(?:RE|FW|FWD|YANIT|YNT|İLT)\s*:\s*)+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ReplySubjectRegex();
}
