using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionGroupFollowUpService(AppDataContext db) : ICollectionGroupFollowUpService
{
    public async Task<ResponseModel<CollectionGroupFollowUpItem>> GetAsync(long contractId, DateOnly period,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPeriod(contractId, period)) return Fail("Geçerli sözleşme ve muhasebe dönemi gereklidir.");
        if (!await GroupContractExistsAsync(contractId, cancellationToken))
            return Fail("Gruba bağlı sözleşme bulunamadı.", StatusCode.NotFound);
        var row = await db.Set<CollectionContractPeriodFollowUp>().AsNoTracking()
            .Where(x => x.ContractId == contractId && x.Period == period && !x.IsDeleted)
            .Select(x => new CollectionGroupFollowUpItem(x.ContractId, x.Period, x.GroupStatusId,
                x.GroupStatus == null ? null : x.GroupStatus.Name, x.Description, x.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);
        return ResponseModel<CollectionGroupFollowUpItem>.Success(
            row ?? new(contractId, period, null, null, null, null), "Grup dönem durumu getirildi.");
    }

    public async Task<ResponseModel<CollectionGroupFollowUpItem>> SaveAsync(long contractId,
        CollectionGroupFollowUpUpdate command, long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<ValidationResult>();
        if (!ValidPeriod(contractId, command.Period)
            || !Validator.TryValidateObject(command, new ValidationContext(command), errors, true))
            return Fail(errors.FirstOrDefault()?.ErrorMessage ?? "Geçerli grup dönem bilgisi gereklidir.");
        if (actorId <= 0) return Fail("Geçerli kullanıcı gereklidir.", StatusCode.Unauthorized);
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            return Fail("İşlem şu anda tamamlanamıyor. Lütfen tekrar deneyin.", StatusCode.Conflict);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            try
            {
                if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
                if (!await GroupContractExistsAsync(contractId, cancellationToken))
                    return Fail("Gruba bağlı sözleşme bulunamadı.", StatusCode.NotFound);
                var row = await db.Set<CollectionContractPeriodFollowUp>()
                    .SingleOrDefaultAsync(x => x.ContractId == contractId && x.Period == command.Period && !x.IsDeleted,
                        cancellationToken);
                var status = await db.Set<CollectionGroupStatus>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == command.GroupStatusId, cancellationToken);
                if (status is null || !status.IsActive && row?.GroupStatusId != status.Id)
                    return Fail("Grup durum etiketi kullanıma açık değil.");
                var now = DateTimeOffset.UtcNow;
                if (row is null)
                {
                    if (command.RowVersion is not null)
                        return Fail("Dönem durumu değişmiş olabilir. Kaydı yenileyin.", StatusCode.Conflict);
                    row = new CollectionContractPeriodFollowUp
                    {
                        ContractId = contractId, Period = command.Period,
                        CreatedUser = actorId, CreatedDate = now
                    };
                    new Repository(db).Add(row);
                }
                else
                {
                    if (command.RowVersion is null || !row.RowVersion.SequenceEqual(command.RowVersion))
                        return Fail("Dönem durumu başka bir işlemde değiştirildi. Kaydı yenileyin.", StatusCode.Conflict);
                    row.UpdatedUser = actorId;
                    row.UpdatedDate = now;
                }
                row.GroupStatusId = status.Id;
                row.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
                await new Repository(db).CompleteAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ResponseModel<CollectionGroupFollowUpItem>.Success(
                    new(row.ContractId, row.Period, row.GroupStatusId, status.Name, row.Description, row.RowVersion),
                    "Grup dönem durumu kaydedildi.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Dönem durumu başka bir işlemde değiştirildi. Kaydı yenileyin.", StatusCode.Conflict);
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException or TimeoutException
                || ex is InvalidOperationException && ex.GetBaseException() is SqlException)
            {
                return Fail("İşlem sonucu doğrulanamadı. Tekrar kaydetmeden önce dönem durumunu yenileyin.", StatusCode.Conflict);
            }
            finally
            {
                foreach (var entry in db.ChangeTracker.Entries<CollectionContractPeriodFollowUp>().ToArray())
                    entry.State = EntityState.Detached;
            }
        });
    }

    private Task<bool> GroupContractExistsAsync(long id, CancellationToken token) => db.Set<CollectionContract>()
        .AsNoTracking().AnyAsync(x => x.Id == id && !x.IsDeleted && !x.Customer.IsDeleted
            && x.Customer.CustomerGroupId != null, token);

    private static bool ValidPeriod(long id, DateOnly period) => id > 0 && period != default
        && period.Day == 1 && period.Year < 9999;

    private static ResponseModel<CollectionGroupFollowUpItem> Fail(string message, StatusCode code = StatusCode.BadRequest) =>
        ResponseModel<CollectionGroupFollowUpItem>.Fail(message, code);
}
