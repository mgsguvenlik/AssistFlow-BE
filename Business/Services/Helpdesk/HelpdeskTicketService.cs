using Business.Interfaces;
using Business.Interfaces.Helpdesk;
using Core.Common;
using Core.Enums;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.Helpdesk;
using Model.Dtos.Auth;
using Model.Dtos.Helpdesk;
using System.Net;

namespace Business.Services.Helpdesk;

public sealed partial class HelpdeskTicketService : IHelpdeskTicketService
{
    private const string Admin = "ADMIN";
    private const string Manager = "HELPDESK_MANAGER";
    private const string Lead = "HELPDESK_TEAM_LEAD";
    private const string Agent = "HELPDESK_AGENT";
    private readonly AppDataContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IHelpdeskTicketNumberGenerator _numberGenerator;
    private readonly IOptionsSnapshot<AppSettings> _appSettings;

    public HelpdeskTicketService(AppDataContext db, ICurrentUser currentUser, IHelpdeskTicketNumberGenerator numberGenerator, IOptionsSnapshot<AppSettings> appSettings)
        => (_db, _currentUser, _numberGenerator, _appSettings) = (db, currentUser, numberGenerator, appSettings);

    public async Task<ResponseModel<List<HelpdeskTicketListItemDto>>> GetListAsync(CancellationToken ct = default)
    {
        var user = await RequiredUser(ct);
        if (user is null) return ResponseModel<List<HelpdeskTicketListItemDto>>.Fail("Oturum bulunamadı.", StatusCode.Unauthorized);
        var query = VisibleTickets(user).Where(x => !x.IsDeleted);
        var items = await query.OrderBy(x => x.IsSuspended).ThenBy(x => x.Priority ?? int.MaxValue).ThenByDescending(x => x.CreatedDate)
            .Select(x => new HelpdeskTicketListItemDto { Id = x.Id, TicketNo = x.TicketNo, Subject = x.Subject, RequesterName = x.RequesterName, Status = x.Status, Priority = x.Priority, IsSuspended = x.IsSuspended, CreatedDate = x.CreatedDate, AssignedUsers = x.Assignments.Where(a => a.IsActive).Select(a => a.User.Name).ToList(), AssignedUserIds = x.Assignments.Where(a => a.IsActive).Select(a => a.UserId).ToList() }).ToListAsync(ct);
        return ResponseModel<List<HelpdeskTicketListItemDto>>.Success(items);
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var user = await RequiredUser(ct);
        if (user is null) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Oturum bulunamadı.", StatusCode.Unauthorized);
        var ticket = await VisibleTickets(user).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return ticket is null ? ResponseModel<HelpdeskTicketDetailDto>.Fail("Ticket bulunamadı veya erişim yetkiniz yok.", StatusCode.NotFound) : ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct));
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> CreateAsync(HelpdeskTicketCreateDto dto, CancellationToken ct = default)
    {
        var user = await RequiredUser(ct);
        if (user is null || !CanManage(user)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Bu işlem için yetkiniz yok.", StatusCode.Unauthorized);
        if (string.IsNullOrWhiteSpace(dto.Subject) || string.IsNullOrWhiteSpace(dto.Description) || string.IsNullOrWhiteSpace(dto.RequesterName) || string.IsNullOrWhiteSpace(dto.RequesterEmail)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Konu, açıklama, talep eden adı ve e-posta adresi zorunludur.");
        if (dto.Priority is < 1) return ResponseModel<HelpdeskTicketDetailDto>.Fail("İş sırası pozitif bir sayı olmalıdır.");
        if (!RecipientsAreValid(dto.RequesterEmail, dto.ToRecipients, dto.CcRecipients)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Talep eden, To veya CC alanında geçersiz e-posta adresi bulunuyor.");
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var ticket = new HelpdeskTicket { TicketNo = await _numberGenerator.NextAsync(ct), Subject = dto.Subject.Trim(), Description = dto.Description.Trim(), RequesterName = dto.RequesterName.Trim(), RequesterEmail = dto.RequesterEmail.Trim(), ToRecipients = dto.ToRecipients, CcRecipients = dto.CcRecipients, Priority = dto.Priority, Status = dto.AssignedUserIds.Count == 0 ? HelpdeskTicketStatus.Created : HelpdeskTicketStatus.Assigned, SourceType = HelpdeskTicketSourceType.Manual, CreatedDate = DateTimeOffset.Now, CreatedUser = user.Id };
        _db.HelpdeskTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        try { await ReplaceAssignments(ticket, dto.AssignedUserIds, user.Id, ct); }
        catch (InvalidOperationException ex) { return ResponseModel<HelpdeskTicketDetailDto>.Fail(ex.Message); }
        AddHistory(ticket.Id, "Created", null, null, null, user.Id);
        await _db.SaveChangesAsync(ct);
        if (dto.AssignedUserIds.Count > 0)
        {
            await QueueAssignmentMessagesAsync(ticket, dto.AssignedUserIds, user.Id, ct);
            await _db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct), "Ticket oluşturuldu.", StatusCode.Created);
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> AssignAsync(long id, HelpdeskAssignmentDto dto, CancellationToken ct = default)
    {
        var user = await RequiredUser(ct);
        if (user is null || !CanManage(user)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Bu işlem için yetkiniz yok.", StatusCode.Unauthorized);
        var ticket = await _db.HelpdeskTickets.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (ticket is null) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Ticket bulunamadı.", StatusCode.NotFound);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var previousUserIds = await _db.HelpdeskTicketAssignments.Where(x => x.TicketId == id && x.IsActive).Select(x => x.UserId).ToListAsync(ct);
        try { await ReplaceAssignments(ticket, dto.UserIds, user.Id, ct); }
        catch (InvalidOperationException ex) { return ResponseModel<HelpdeskTicketDetailDto>.Fail(ex.Message); }
        if (dto.UserIds.Count > 0 && ticket.Status == HelpdeskTicketStatus.Created) ticket.Status = HelpdeskTicketStatus.Assigned;
        if (dto.UserIds.Count == 0 && ticket.Status == HelpdeskTicketStatus.Assigned) ticket.Status = HelpdeskTicketStatus.Created;
        AddHistory(id, "AssignmentChanged", null, null, string.Join(',', dto.UserIds), user.Id);
        await _db.SaveChangesAsync(ct);
        var addedUserIds = dto.UserIds.Distinct().Except(previousUserIds).ToList();
        if (addedUserIds.Count > 0)
        {
            await QueueAssignmentMessagesAsync(ticket, addedUserIds, user.Id, ct);
            await _db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct));
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> ChangeStatusAsync(long id, HelpdeskStatusChangeDto dto, CancellationToken ct = default)
    {
        var ticketResult = await ManageableTicket(id, ct); if (ticketResult.ticket is null) return ticketResult.failure!;
        var (ticket, user) = ticketResult.ticket.Value;
        if (!Enum.IsDefined(dto.Status)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Geçersiz ticket durumu.");
        if (dto.Status == HelpdeskTicketStatus.Reopened && !CanReopen(user)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Tamamlanmış ticketı yalnız Helpdesk Yöneticisi veya Admin yeniden açabilir.", StatusCode.Unauthorized);
        if (dto.Status == HelpdeskTicketStatus.Reopened && ticket.Status != HelpdeskTicketStatus.Completed) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Yalnız tamamlanmış ticket yeniden açılabilir.");
        if (!CanManage(user) && !(IsAgent(user) && ticket.Assignments.Any(a => a.IsActive && a.UserId == user.Id) && dto.Status == HelpdeskTicketStatus.Completed)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Bu durum değişikliği için yetkiniz yok.", StatusCode.Unauthorized);
        var previous = ticket.Status; ticket.Status = dto.Status; ticket.CompletedDate = dto.Status == HelpdeskTicketStatus.Completed ? DateTimeOffset.Now : null; ticket.UpdatedDate = DateTimeOffset.Now; ticket.UpdatedUser = user.Id;
        AddHistory(id, "StatusChanged", dto.Description, previous.ToString(), dto.Status.ToString(), user.Id); await _db.SaveChangesAsync(ct);
        return ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct));
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> ChangePriorityAsync(long id, HelpdeskPriorityChangeDto dto, CancellationToken ct = default)
    {
        var ticketResult = await ManageableTicket(id, ct); if (ticketResult.ticket is null) return ticketResult.failure!;
        var (ticket, user) = ticketResult.ticket.Value; if (!CanManage(user) || dto.Priority is < 1) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Geçerli pozitif bir iş sırası ve yönetim yetkisi gerekir.", StatusCode.Unauthorized);
        var previous = ticket.Priority; ticket.Priority = dto.Priority; ticket.UpdatedDate = DateTimeOffset.Now; ticket.UpdatedUser = user.Id; AddHistory(id, "PriorityChanged", null, previous?.ToString(), dto.Priority?.ToString(), user.Id); await _db.SaveChangesAsync(ct);
        return ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct));
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> SuspendAsync(long id, HelpdeskSuspendDto dto, CancellationToken ct = default)
    {
        var ticketResult = await ManageableTicket(id, ct); if (ticketResult.ticket is null) return ticketResult.failure!;
        var (ticket, user) = ticketResult.ticket.Value; if (!CanManage(user)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Bu işlem için yetkiniz yok.", StatusCode.Unauthorized); if (dto.SuspendedUntil <= DateTimeOffset.Now) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Askı bitiş tarihi gelecekte olmalıdır.");
        ticket.IsSuspended = true; ticket.SuspendedUntil = dto.SuspendedUntil; AddHistory(id, "Suspended", dto.Description, null, dto.SuspendedUntil?.ToString("O"), user.Id); await _db.SaveChangesAsync(ct); return ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct));
    }

    public async Task<ResponseModel<HelpdeskTicketDetailDto>> UnsuspendAsync(long id, CancellationToken ct = default)
    {
        var ticketResult = await ManageableTicket(id, ct); if (ticketResult.ticket is null) return ticketResult.failure!;
        var (ticket, user) = ticketResult.ticket.Value; if (!CanManage(user)) return ResponseModel<HelpdeskTicketDetailDto>.Fail("Bu işlem için yetkiniz yok.", StatusCode.Unauthorized);
        ticket.IsSuspended = false; ticket.SuspendedUntil = null; AddHistory(id, "Unsuspended", null, null, null, user.Id); await _db.SaveChangesAsync(ct); return ResponseModel<HelpdeskTicketDetailDto>.Success(await Detail(ticket, ct));
    }

    public async Task<ResponseModel<HelpdeskCommentDto>> AddCommentAsync(long id, HelpdeskCommentCreateDto dto, CancellationToken ct = default)
    {
        var ticketResult = await ManageableTicket(id, ct); if (ticketResult.ticket is null) return ResponseModel<HelpdeskCommentDto>.Fail(ticketResult.failure!.Message!, ticketResult.failure.StatusCode);
        var (ticket, user) = ticketResult.ticket.Value; if (!CanManage(user) && !ticket.Assignments.Any(a => a.IsActive && a.UserId == user.Id)) return ResponseModel<HelpdeskCommentDto>.Fail("Bu ticket için yetkiniz yok.", StatusCode.Unauthorized);
        if (string.IsNullOrWhiteSpace(dto.Body)) return ResponseModel<HelpdeskCommentDto>.Fail("Yorum boş olamaz.");
        var comment = new HelpdeskTicketComment { TicketId = id, Body = dto.Body.Trim(), CreatedDate = DateTimeOffset.Now, CreatedUser = user.Id }; _db.HelpdeskTicketComments.Add(comment); AddHistory(id, "CommentAdded", null, null, null, user.Id); await _db.SaveChangesAsync(ct);
        return ResponseModel<HelpdeskCommentDto>.Success(new HelpdeskCommentDto { Id = comment.Id, Body = comment.Body, CreatedDate = comment.CreatedDate, CreatedUser = user.Id });
    }

    public async Task<ResponseModel<List<HelpdeskTicketMailDto>>> GetMailsAsync(long id, CancellationToken ct = default)
    {
        var access = await GetAsync(id, ct);
        if (!access.IsSuccess) return ResponseModel<List<HelpdeskTicketMailDto>>.Fail(access.Message, access.StatusCode);
        var data = await _db.HelpdeskTicketMails.AsNoTracking().Where(x => x.TicketId == id && !x.IsDeleted)
            .OrderBy(x => x.MailDate).Select(x => new HelpdeskTicketMailDto { Id = x.Id, Direction = x.Direction, FromAddress = x.FromAddress, SenderName = x.Direction == HelpdeskMailDirection.Outgoing ? _db.Users.Where(u => u.Id == x.CreatedUser).Select(u => u.Name).FirstOrDefault() : null, ToRecipients = x.ToRecipients, CcRecipients = x.CcRecipients, Subject = x.Subject, Body = x.Body, MailDate = x.MailDate }).ToListAsync(ct);
        return ResponseModel<List<HelpdeskTicketMailDto>>.Success(data);
    }

    public async Task<ResponseModel<List<HelpdeskCommentDto>>> GetCommentsAsync(long id, CancellationToken ct = default)
    {
        var access = await GetAsync(id, ct);
        return !access.IsSuccess
            ? ResponseModel<List<HelpdeskCommentDto>>.Fail(access.Message, access.StatusCode)
            : ResponseModel<List<HelpdeskCommentDto>>.Success(access.Data!.Comments);
    }

    public async Task<ResponseModel<List<HelpdeskHistoryDto>>> GetHistoryAsync(long id, CancellationToken ct = default)
    {
        var access = await GetAsync(id, ct);
        return !access.IsSuccess
            ? ResponseModel<List<HelpdeskHistoryDto>>.Fail(access.Message, access.StatusCode)
            : ResponseModel<List<HelpdeskHistoryDto>>.Success(access.Data!.History);
    }

    public async Task<ResponseModel<HelpdeskTicketMailDto>> ReplyAsync(long id, HelpdeskReplyDto dto, CancellationToken ct = default)
    {
        var result = await ManageableTicket(id, ct);
        if (result.ticket is null) return ResponseModel<HelpdeskTicketMailDto>.Fail(result.failure!.Message, result.failure.StatusCode);
        var (ticket, user) = result.ticket.Value;
        if (string.IsNullOrWhiteSpace(dto.Body)) return ResponseModel<HelpdeskTicketMailDto>.Fail("Mail içeriği boş olamaz.");
        if (string.IsNullOrWhiteSpace(ticket.RequesterEmail)) return ResponseModel<HelpdeskTicketMailDto>.Fail("Talep eden e-posta adresi bulunamadı.");
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var latest = await _db.HelpdeskTicketMails.AsNoTracking().Where(x => x.TicketId == id).OrderByDescending(x => x.MailDate).FirstOrDefaultAsync(ct);
        var mailboxAddress = ticket.MailboxId.HasValue ? await _db.HelpdeskMailboxes.Where(x => x.Id == ticket.MailboxId).Select(x => x.Address).FirstOrDefaultAsync(ct) : null;
        var cc = string.Join(';', SplitRecipients(ticket.ToRecipients, ticket.CcRecipients)
            .Where(x => !string.Equals(x, ticket.RequesterEmail, StringComparison.OrdinalIgnoreCase) && !string.Equals(x, mailboxAddress, StringComparison.OrdinalIgnoreCase)));
        var messageId = $"<{Guid.NewGuid():N}@assistflow.helpdesk>";
        var references = string.Join(' ', new[] { latest?.References, latest?.MessageId }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var subject = $"[{ticket.TicketNo}] {TicketPrefixRegex().Replace(ticket.Subject, string.Empty).Trim()}";
        var senderName = WebUtility.HtmlEncode(user.Name);
        var body = $"<p>{WebUtility.HtmlEncode(dto.Body.Trim()).Replace("\r\n", "<br>").Replace("\n", "<br>")}</p><p style=\"margin-top:24px\">Saygılarımızla,<br><strong>{senderName}</strong><br>FlowAssist Helpdesk</p>";
        var entity = new HelpdeskTicketMail { TicketId = id, MailboxId = ticket.MailboxId, MessageId = messageId, InReplyTo = latest?.MessageId, References = references, Direction = HelpdeskMailDirection.Outgoing, ToRecipients = ticket.RequesterEmail, CcRecipients = cc, Subject = subject, Body = body, MailDate = DateTimeOffset.Now, CreatedDate = DateTimeOffset.Now, CreatedUser = user.Id };
        _db.HelpdeskTicketMails.Add(entity);
        _db.MailOutboxes.Add(new MailOutbox { RequestNo = ticket.TicketNo, ToRecipients = ticket.RequesterEmail, CcRecipients = cc, Subject = subject, BodyHtml = body, MessageId = messageId, InReplyTo = latest?.MessageId, References = references, Status = MailOutboxStatus.Pending, CreatedDate = DateTime.Now, CreatedUser = user.Id });
        AddHistory(id, "ReplyQueued", subject, null, null, user.Id);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ResponseModel<HelpdeskTicketMailDto>.Success(new HelpdeskTicketMailDto { Id = entity.Id, Direction = entity.Direction, SenderName = user.Name, ToRecipients = entity.ToRecipients, CcRecipients = entity.CcRecipients, Subject = entity.Subject, Body = entity.Body, MailDate = entity.MailDate });
    }

    private IQueryable<HelpdeskTicket> VisibleTickets(CurrentUserDto user) => CanManage(user) ? _db.HelpdeskTickets : _db.HelpdeskTickets.Where(x => x.Assignments.Any(a => a.IsActive && a.UserId == user.Id));
    private static bool CanManage(CurrentUserDto user) => user.Roles.Any(r => string.Equals(r.Code, Admin, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Code, Manager, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Code, Lead, StringComparison.OrdinalIgnoreCase));
    private static bool CanReopen(CurrentUserDto user) => user.Roles.Any(r => string.Equals(r.Code, Admin, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Code, Manager, StringComparison.OrdinalIgnoreCase));
    private static bool IsAgent(CurrentUserDto user) => user.Roles.Any(r => string.Equals(r.Code, Agent, StringComparison.OrdinalIgnoreCase));
    private async Task<CurrentUserDto?> RequiredUser(CancellationToken ct) => await _currentUser.GetAsync(ct);
    private void AddHistory(long ticketId, string action, string? description, string? previous, string? next, long userId) => _db.HelpdeskTicketHistories.Add(new HelpdeskTicketHistory { TicketId = ticketId, Action = action, Description = description, PreviousValue = previous, NewValue = next, CreatedDate = DateTimeOffset.Now, CreatedUser = userId });
    private async Task ReplaceAssignments(HelpdeskTicket ticket, IEnumerable<long> users, long actorId, CancellationToken ct) { var ids = users.Distinct().ToList(); var valid = await _db.Users.Where(x => ids.Contains(x.Id) && x.IsActive && !x.IsDeleted && x.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == Agent)).Select(x => x.Id).ToListAsync(ct); if (valid.Count != ids.Count) throw new InvalidOperationException("Yalnız aktif Helpdesk Personel kullanıcıları atanabilir."); var active = await _db.HelpdeskTicketAssignments.Where(x => x.TicketId == ticket.Id && x.IsActive).ToListAsync(ct); foreach (var assignment in active.Where(x => !valid.Contains(x.UserId))) assignment.IsActive = false; foreach (var userId in valid.Except(active.Select(x => x.UserId))) _db.HelpdeskTicketAssignments.Add(new HelpdeskTicketAssignment { TicketId = ticket.Id, UserId = userId, IsActive = true, CreatedDate = DateTimeOffset.Now, CreatedUser = actorId }); }
    private async Task QueueAssignmentMessagesAsync(HelpdeskTicket ticket, IEnumerable<long> userIds, long actorId, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Id, x.Email }).ToListAsync(ct);
        foreach (var assigned in users)
            _db.Notifications.Add(new Notification { Type = NotificationType.WorkflowAssigned, Scope = NotificationScope.User, TargetUserId = assigned.Id, RequestNo = ticket.TicketNo, Title = "Yeni Helpdesk talebi", Message = $"{ticket.TicketNo} numaralı talep tarafınıza atandı.", CreatedDate = DateTime.Now, CreatedUser = actorId });
        var personnelRecipients = string.Join(';', users.Select(x => x.Email).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(personnelRecipients))
        {
            var detailUrl = $"{_appSettings.Value.FrontUrl.TrimEnd('/')}/helpdesk/tickets/{ticket.Id}";
            _db.MailOutboxes.Add(new MailOutbox
            {
                RequestNo = ticket.TicketNo,
                ToRecipients = personnelRecipients,
                Subject = $"Yeni Helpdesk Talebi Oluşturuldu - {ticket.TicketNo}",
                BodyHtml = BuildAssignmentMailBody(ticket, detailUrl),
                Status = MailOutboxStatus.Pending,
                CreatedDate = DateTime.Now,
                CreatedUser = actorId
            });
        }

        var acceptedAt = DateTimeOffset.Now;
        var claimed = await _db.HelpdeskTickets.Where(x => x.Id == ticket.Id && x.AcceptanceMailSentAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AcceptanceMailSentAt, acceptedAt), ct);
        if (claimed == 0) return;
        ticket.AcceptanceMailSentAt = acceptedAt;
        var latestIncoming = await _db.HelpdeskTicketMails.AsNoTracking().Where(x => x.TicketId == ticket.Id && x.Direction == HelpdeskMailDirection.Incoming)
            .OrderByDescending(x => x.MailDate).FirstOrDefaultAsync(ct);
        var messageId = $"<{Guid.NewGuid():N}@assistflow.helpdesk>";
        var references = string.Join(' ', new[] { latestIncoming?.References, latestIncoming?.MessageId }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var mailboxAddress = ticket.MailboxId.HasValue
            ? await _db.HelpdeskMailboxes.Where(x => x.Id == ticket.MailboxId.Value).Select(x => x.Address).FirstOrDefaultAsync(ct)
            : null;
        var cc = SplitRecipients(ticket.ToRecipients, ticket.CcRecipients)
            .Where(x => !string.Equals(x, ticket.RequesterEmail, StringComparison.OrdinalIgnoreCase) && !string.Equals(x, mailboxAddress, StringComparison.OrdinalIgnoreCase));
        var ccRecipients = string.Join(';', cc);
        var subject = $"[{ticket.TicketNo}] Talebiniz alınmıştır";
        var body = BuildAcceptanceMailBody(ticket);
        _db.MailOutboxes.Add(new MailOutbox { RequestNo = ticket.TicketNo, ToRecipients = ticket.RequesterEmail, CcRecipients = ccRecipients, Subject = subject, BodyHtml = body, MessageId = messageId, InReplyTo = latestIncoming?.MessageId, References = references, Status = MailOutboxStatus.Pending, CreatedDate = DateTime.Now, CreatedUser = actorId });
        _db.HelpdeskTicketMails.Add(new HelpdeskTicketMail { TicketId = ticket.Id, MailboxId = ticket.MailboxId, MessageId = messageId, InReplyTo = latestIncoming?.MessageId, References = references, Direction = HelpdeskMailDirection.Outgoing, ToRecipients = ticket.RequesterEmail, CcRecipients = ccRecipients, Subject = subject, Body = body, MailDate = acceptedAt, CreatedDate = acceptedAt, CreatedUser = actorId });
        AddHistory(ticket.Id, "AcceptanceMailQueued", null, null, acceptedAt.ToString("O"), actorId);
    }
    private static string BuildAcceptanceMailBody(HelpdeskTicket ticket)
    {
        var ticketNo = WebUtility.HtmlEncode(ticket.TicketNo);
        var requesterName = WebUtility.HtmlEncode(ticket.RequesterName);

        return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8' /></head>
<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8;padding:24px 0;'>
        <tr><td align='center'>
            <table width='640' cellpadding='0' cellspacing='0' style='width:100%;max-width:640px;background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;'>
                <tr><td style='background-color:#1f4e79;color:#ffffff;padding:20px 24px;'>
                    <h2 style='margin:0;font-size:20px;'>Helpdesk Talebiniz Alındı</h2>
                </td></tr>
                <tr><td style='padding:24px;'>
                    <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>Sayın {requesterName},</p>
                    <p style='font-size:15px;line-height:1.7;margin:0 0 18px;'>Bize iletmiş olduğunuz talebiniz başarıyla alınmış ve aşağıdaki talep numarasıyla kayıt altına alınmıştır.</p>
                    <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin:20px 0;'>
                        <tr>
                            <td style='padding:12px;border:1px solid #e5e7eb;background:#f9fafb;width:180px;'><strong>Talep Numaranız</strong></td>
                            <td style='padding:12px;border:1px solid #e5e7eb;font-weight:600;color:#1f4e79;'>{ticketNo}</td>
                        </tr>
                    </table>
                    <p style='font-size:15px;line-height:1.7;margin:0 0 16px;'>İlgili ekibimiz talebinizi inceleyerek en kısa sürede sizinle iletişime geçecektir.</p>
                    <p style='font-size:15px;line-height:1.7;margin:0;'>İlginiz ve anlayışınız için teşekkür ederiz.</p>
                    <p style='font-size:13px;line-height:1.6;margin:24px 0 0;color:#6b7280;'>Bu bildirim FlowAssist Helpdesk sistemi tarafından otomatik olarak oluşturulmuştur.</p>
                </td></tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
    }
    private static string BuildAssignmentMailBody(HelpdeskTicket ticket, string detailUrl)
    {
        var ticketNo = WebUtility.HtmlEncode(ticket.TicketNo);
        var subject = WebUtility.HtmlEncode(ticket.Subject);
        var requesterName = WebUtility.HtmlEncode(ticket.RequesterName);
        var requesterEmail = WebUtility.HtmlEncode(ticket.RequesterEmail);
        var priority = ticket.Priority?.ToString() ?? "Belirtilmedi";
        var createdDate = ticket.CreatedDate.ToString("dd.MM.yyyy HH:mm");
        var description = MailDescriptionAsHtml(ticket.Description);
        var safeDetailUrl = WebUtility.HtmlEncode(detailUrl);

        return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8' /></head>
<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8;padding:24px 0;'>
        <tr><td align='center'>
            <table width='640' cellpadding='0' cellspacing='0' style='width:100%;max-width:640px;background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;'>
                <tr><td style='background-color:#1f4e79;color:#ffffff;padding:20px 24px;'>
                    <h2 style='margin:0;font-size:20px;'>Yeni Helpdesk Talebi Oluşturuldu</h2>
                </td></tr>
                <tr><td style='padding:24px;'>
                    <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>Merhaba,</p>
                    <p style='font-size:15px;line-height:1.6;margin:0 0 20px;'>Tarafınıza <strong>{ticketNo}</strong> numaralı Helpdesk talebi atandı. Talep detayları aşağıdadır.</p>
                    <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin-top:12px;'>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;width:180px;'><strong>Talep No</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{ticketNo}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Başlık</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{subject}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Talep Eden</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{requesterName}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>E-posta</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'><a href='mailto:{requesterEmail}' style='color:#2563eb;'>{requesterEmail}</a></td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Öncelik</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{priority}</td></tr>
                        <tr><td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Oluşturma Tarihi</strong></td><td style='padding:10px;border:1px solid #e5e7eb;'>{createdDate}</td></tr>
                    </table>
                    <div style='margin-top:20px;padding:14px;background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px;'>
                        <strong>Açıklama:</strong><p style='margin:8px 0 0;line-height:1.6;'>{description}</p>
                    </div>
                    <p style='font-size:14px;line-height:1.6;margin:22px 0 14px;color:#6b7280;'>Talebi incelemek ve işlem yapmak için aşağıdaki bağlantıyı kullanabilirsiniz.</p>
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
    private static string MailDescriptionAsHtml(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "-";

        var text = System.Text.RegularExpressions.Regex.Replace(description, @"<(script|style)\b[^>]*>[\s\S]*?</\1>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<\s*(br\s*/?|/p|/div|/li)\s*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x));
        var plainText = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(plainText) ? "-" : WebUtility.HtmlEncode(plainText).Replace("\n", "<br>");
    }
    private static IEnumerable<string> SplitRecipients(params string?[] values) => values.SelectMany(x => (x ?? string.Empty).Replace(',', ';').Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.OrdinalIgnoreCase);
    private static bool RecipientsAreValid(params string?[] values) { try { foreach (var recipient in SplitRecipients(values)) _ = new System.Net.Mail.MailAddress(recipient); return true; } catch (FormatException) { return false; } }
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*\[?HD-\d{4}-\d{6}\]?\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex TicketPrefixRegex();
    private async Task<HelpdeskTicketDetailDto> Detail(HelpdeskTicket ticket, CancellationToken ct) { var assignments = await _db.HelpdeskTicketAssignments.Where(x => x.TicketId == ticket.Id && x.IsActive).Select(x => new { x.UserId, x.User.Name }).ToListAsync(ct); var dto = new HelpdeskTicketDetailDto { Id = ticket.Id, TicketNo = ticket.TicketNo, Subject = ticket.Subject, Description = ticket.Description, RequesterName = ticket.RequesterName, RequesterEmail = ticket.RequesterEmail, ToRecipients = ticket.ToRecipients, CcRecipients = ticket.CcRecipients, Status = ticket.Status, Priority = ticket.Priority, IsSuspended = ticket.IsSuspended, SuspendedUntil = ticket.SuspendedUntil, CompletedDate = ticket.CompletedDate, CreatedDate = ticket.CreatedDate, AssignedUsers = assignments.Select(x => x.Name).ToList(), AssignedUserIds = assignments.Select(x => x.UserId).ToList(), Comments = await _db.HelpdeskTicketComments.Where(x => x.TicketId == ticket.Id && !x.IsDeleted).OrderBy(x => x.CreatedDate).Select(x => new HelpdeskCommentDto { Id = x.Id, Body = x.Body, CreatedDate = x.CreatedDate, CreatedUser = x.CreatedUser }).ToListAsync(ct), History = await _db.HelpdeskTicketHistories.Where(x => x.TicketId == ticket.Id && !x.IsDeleted).OrderByDescending(x => x.CreatedDate).Select(x => new HelpdeskHistoryDto { Id = x.Id, Action = x.Action, Description = x.Description, PreviousValue = x.PreviousValue, NewValue = x.NewValue, CreatedDate = x.CreatedDate, CreatedUser = x.CreatedUser }).ToListAsync(ct) }; return dto; }
    private async Task<((HelpdeskTicket ticket, CurrentUserDto user)? ticket, ResponseModel<HelpdeskTicketDetailDto>? failure)> ManageableTicket(long id, CancellationToken ct) { var user = await RequiredUser(ct); if (user is null) return (null, ResponseModel<HelpdeskTicketDetailDto>.Fail("Oturum bulunamadı.", StatusCode.Unauthorized)); var ticket = await VisibleTickets(user).Include(x => x.Assignments).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct); return ticket is null ? (null, ResponseModel<HelpdeskTicketDetailDto>.Fail("Ticket bulunamadı veya erişim yetkiniz yok.", StatusCode.NotFound)) : ((ticket, user), null); }
}
