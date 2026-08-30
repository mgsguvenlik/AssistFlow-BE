using Business.Interfaces;
using Business.Interfaces.Helpdesk;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Helpdesk;
using Model.Dtos.Auth;
using Model.Dtos.Helpdesk;
using System.Net.Mail;

namespace Business.Services.Helpdesk;

public sealed class HelpdeskMailboxService : IHelpdeskMailboxService
{
    private static readonly string[] ManagementRoles = ["ADMIN", "HELPDESK_MANAGER", "HELPDESK_TEAM_LEAD"];
    private readonly AppDataContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IHelpdeskSecretProtector _protector;

    public HelpdeskMailboxService(AppDataContext db, ICurrentUser currentUser, IHelpdeskSecretProtector protector)
        => (_db, _currentUser, _protector) = (db, currentUser, protector);

    public async Task<ResponseModel<List<HelpdeskMailboxDto>>> GetMailboxesAsync(CancellationToken ct = default)
    {
        if (!await CanManageAsync(ct)) return Forbidden<List<HelpdeskMailboxDto>>();
        var data = await _db.HelpdeskMailboxes.AsNoTracking().Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name).Select(x => ToDto(x)).ToListAsync(ct);
        return ResponseModel<List<HelpdeskMailboxDto>>.Success(data);
    }

    public async Task<ResponseModel<HelpdeskMailboxDto>> CreateMailboxAsync(HelpdeskMailboxCreateDto dto, CancellationToken ct = default)
    {
        var user = await ManagerAsync(ct);
        if (user is null) return Forbidden<HelpdeskMailboxDto>();
        var validation = ValidateMailbox(dto.Name, dto.Address, dto.Username, dto.Password, dto.EwsUrl);
        if (validation is not null) return ResponseModel<HelpdeskMailboxDto>.Fail(validation);
        var address = dto.Address.Trim().ToLowerInvariant();
        if (await _db.HelpdeskMailboxes.AnyAsync(x => !x.IsDeleted && x.Address == address, ct))
            return ResponseModel<HelpdeskMailboxDto>.Fail("Bu mailbox adresi zaten tanımlı.", StatusCode.Conflict);
        var entity = new HelpdeskMailbox
        {
            Name = dto.Name.Trim(), Address = address, Username = dto.Username.Trim(),
            ProtectedPassword = _protector.Protect(dto.Password), EwsUrl = dto.EwsUrl.Trim(), IsActive = dto.IsActive,
            CreatedDate = DateTimeOffset.Now, CreatedUser = user.Id
        };
        _db.HelpdeskMailboxes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ResponseModel<HelpdeskMailboxDto>.Success(ToDto(entity), "Mailbox oluşturuldu.", StatusCode.Created);
    }

    public async Task<ResponseModel<HelpdeskMailboxDto>> UpdateMailboxAsync(long id, HelpdeskMailboxUpdateDto dto, CancellationToken ct = default)
    {
        var user = await ManagerAsync(ct);
        if (user is null) return Forbidden<HelpdeskMailboxDto>();
        var entity = await _db.HelpdeskMailboxes.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return ResponseModel<HelpdeskMailboxDto>.Fail("Mailbox bulunamadı.", StatusCode.NotFound);
        var validation = ValidateMailbox(dto.Name, dto.Address, dto.Username, dto.Password ?? "preserved", dto.EwsUrl);
        if (validation is not null) return ResponseModel<HelpdeskMailboxDto>.Fail(validation);
        var address = dto.Address.Trim().ToLowerInvariant();
        if (await _db.HelpdeskMailboxes.AnyAsync(x => x.Id != id && !x.IsDeleted && x.Address == address, ct))
            return ResponseModel<HelpdeskMailboxDto>.Fail("Bu mailbox adresi zaten tanımlı.", StatusCode.Conflict);
        entity.Name = dto.Name.Trim(); entity.Address = address; entity.Username = dto.Username.Trim();
        entity.EwsUrl = dto.EwsUrl.Trim();
        entity.IsActive = dto.IsActive; entity.UpdatedDate = DateTimeOffset.Now; entity.UpdatedUser = user.Id;
        if (!string.IsNullOrWhiteSpace(dto.Password)) entity.ProtectedPassword = _protector.Protect(dto.Password);
        await _db.SaveChangesAsync(ct);
        return ResponseModel<HelpdeskMailboxDto>.Success(ToDto(entity));
    }

    public async Task<ResponseModel<bool>> DeleteMailboxAsync(long id, CancellationToken ct = default)
    {
        var user = await ManagerAsync(ct);
        if (user is null) return Forbidden<bool>();
        var entity = await _db.HelpdeskMailboxes.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return ResponseModel<bool>.Fail("Mailbox bulunamadı.", StatusCode.NotFound);
        if (await _db.HelpdeskTickets.AnyAsync(x => x.MailboxId == id && !x.IsDeleted, ct))
            return ResponseModel<bool>.Fail("Ticket geçmişi bulunan mailbox silinemez; pasif hale getirin.", StatusCode.Conflict);
        entity.IsDeleted = true; entity.IsActive = false; entity.UpdatedDate = DateTimeOffset.Now; entity.UpdatedUser = user.Id;
        await _db.SaveChangesAsync(ct);
        return ResponseModel<bool>.Success(true);
    }

    public async Task<ResponseModel<List<HelpdeskMailRuleDto>>> GetRulesAsync(long mailboxId, CancellationToken ct = default)
    {
        if (!await CanManageAsync(ct)) return Forbidden<List<HelpdeskMailRuleDto>>();
        var data = await _db.HelpdeskMailRules.AsNoTracking().Where(x => x.MailboxId == mailboxId && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).Select(x => ToDto(x)).ToListAsync(ct);
        return ResponseModel<List<HelpdeskMailRuleDto>>.Success(data);
    }

    public async Task<ResponseModel<HelpdeskMailRuleDto>> CreateRuleAsync(HelpdeskMailRuleCreateDto dto, CancellationToken ct = default)
    {
        var user = await ManagerAsync(ct);
        if (user is null) return Forbidden<HelpdeskMailRuleDto>();
        var validation = await ValidateRuleAsync(dto, null, ct);
        if (validation is not null) return ResponseModel<HelpdeskMailRuleDto>.Fail(validation);
        var entity = new HelpdeskMailRule { MailboxId = dto.MailboxId, Field = dto.Field, Operator = dto.Operator, Value = dto.Value.Trim(), LogicalOperator = dto.LogicalOperator, SortOrder = dto.SortOrder, IsActive = dto.IsActive, CreatedDate = DateTimeOffset.Now, CreatedUser = user.Id };
        _db.HelpdeskMailRules.Add(entity); await _db.SaveChangesAsync(ct);
        return ResponseModel<HelpdeskMailRuleDto>.Success(ToDto(entity), "Mail kuralı oluşturuldu.", StatusCode.Created);
    }

    public async Task<ResponseModel<HelpdeskMailRuleDto>> UpdateRuleAsync(long id, HelpdeskMailRuleUpdateDto dto, CancellationToken ct = default)
    {
        var user = await ManagerAsync(ct);
        if (user is null) return Forbidden<HelpdeskMailRuleDto>();
        var entity = await _db.HelpdeskMailRules.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return ResponseModel<HelpdeskMailRuleDto>.Fail("Mail kuralı bulunamadı.", StatusCode.NotFound);
        var validation = await ValidateRuleAsync(dto, id, ct);
        if (validation is not null) return ResponseModel<HelpdeskMailRuleDto>.Fail(validation);
        entity.MailboxId = dto.MailboxId; entity.Field = dto.Field; entity.Operator = dto.Operator;
        entity.Value = dto.Value.Trim(); entity.LogicalOperator = dto.LogicalOperator; entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive; entity.UpdatedDate = DateTimeOffset.Now; entity.UpdatedUser = user.Id;
        await _db.SaveChangesAsync(ct); return ResponseModel<HelpdeskMailRuleDto>.Success(ToDto(entity));
    }

    public async Task<ResponseModel<bool>> DeleteRuleAsync(long id, CancellationToken ct = default)
    {
        var user = await ManagerAsync(ct);
        if (user is null) return Forbidden<bool>();
        var entity = await _db.HelpdeskMailRules.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return ResponseModel<bool>.Fail("Mail kuralı bulunamadı.", StatusCode.NotFound);
        entity.IsDeleted = true; entity.IsActive = false; entity.UpdatedDate = DateTimeOffset.Now; entity.UpdatedUser = user.Id;
        await _db.SaveChangesAsync(ct); return ResponseModel<bool>.Success(true);
    }

    private async Task<CurrentUserDto?> ManagerAsync(CancellationToken ct)
    {
        var user = await _currentUser.GetAsync(ct);
        return user is not null && IsManager(user) ? user : null;
    }

    private async Task<bool> CanManageAsync(CancellationToken ct) => await ManagerAsync(ct) is not null;
    private static bool IsManager(CurrentUserDto user) => user.Roles.Any(r => r.Code is not null && ManagementRoles.Contains(r.Code, StringComparer.OrdinalIgnoreCase));
    private static ResponseModel<T> Forbidden<T>() => ResponseModel<T>.Fail("Helpdesk mailbox yönetimi için yetkiniz yok.", StatusCode.Unauthorized);
    private static string? ValidateMailbox(string name, string address, string username, string password, string ewsUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(ewsUrl)) return "Mailbox alanları zorunludur.";
        try { _ = new MailAddress(address); } catch (FormatException) { return "Mailbox e-posta adresi geçersiz."; }
        return !Uri.TryCreate(ewsUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ? "EWS URL geçerli bir HTTPS adresi olmalıdır." : null;
    }

    private async Task<string?> ValidateRuleAsync(HelpdeskMailRuleCreateDto dto, long? id, CancellationToken ct)
    {
        if (!await _db.HelpdeskMailboxes.AnyAsync(x => x.Id == dto.MailboxId && !x.IsDeleted, ct)) return "Mailbox bulunamadı.";
        if (string.IsNullOrWhiteSpace(dto.Value)) return "Kural değeri zorunludur.";
        if (dto.SortOrder < 0) return "Kural sırası negatif olamaz.";
        if (!Enum.IsDefined(dto.Field)) return "Geçersiz kural alanı.";
        if (!Enum.IsDefined(dto.Operator)) return "Geçersiz karşılaştırma operatörü.";
        return null;
    }

    private static HelpdeskMailboxDto ToDto(HelpdeskMailbox x) => new() { Id = x.Id, Name = x.Name, Address = x.Address, Username = x.Username, EwsUrl = x.EwsUrl, IsActive = x.IsActive, HasPassword = !string.IsNullOrEmpty(x.ProtectedPassword) };
    private static HelpdeskMailRuleDto ToDto(HelpdeskMailRule x) => new() { Id = x.Id, MailboxId = x.MailboxId, Field = x.Field, Operator = x.Operator, Value = x.Value, LogicalOperator = x.LogicalOperator, SortOrder = x.SortOrder, IsActive = x.IsActive };
}
