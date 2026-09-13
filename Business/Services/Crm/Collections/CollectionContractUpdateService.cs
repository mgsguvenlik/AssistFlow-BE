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

public sealed class CollectionContractUpdateService(AppDataContext db) : ICollectionContractUpdateService
{
    public async Task<ResponseModel<CollectionContractIdentityUpdated>> UpdateIdentityAsync(long id,
        CollectionContractIdentityUpdate command, long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (actorId <= 0) return Fail("Geçerli kullanıcı gereklidir.", StatusCode.Unauthorized);
        var errors = new List<ValidationResult>();
        if (id <= 0 || !Validator.TryValidateObject(command, new ValidationContext(command), errors, true))
            return Fail(errors.Count == 0 ? "Geçersiz sözleşme kimliği." : string.Join(" ", errors.Select(x => x.ErrorMessage)));
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            return Fail("İşlem şu anda tamamlanamıyor. Lütfen tekrar deneyin.", StatusCode.Conflict);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            CollectionContract? contract = null;
            try
            {
                if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
                contract = await db.Set<CollectionContract>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
                if (contract is null) return Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
                if (!contract.RowVersion.SequenceEqual(command.RowVersion))
                    return Fail("Sözleşme değişmiş veya önceki kayıt tamamlanmış olabilir. Detayı yenileyip tekrar kontrol edin.", StatusCode.Conflict);
                // An unchanged historical reference can remain even when no longer selectable.
                if (command.ServiceTypeId != contract.ServiceTypeId && !await db.ServiceTypes.AsNoTracking()
                    .AnyAsync(x => x.Id == command.ServiceTypeId && !x.IsDeleted, cancellationToken))
                    return Fail("Seçilen servis tipi kullanıma açık değil.");
                contract.ServiceTypeId = command.ServiceTypeId;
                contract.GtsNo = command.GtsNo;
                contract.IvrNo = command.IvrNo;
                contract.UpdatedUser = actorId;
                contract.UpdatedDate = DateTimeOffset.UtcNow;
                await new Repository(db).CompleteAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return ResponseModel<CollectionContractIdentityUpdated>.Success(new(id), "Sözleşme bilgileri başarıyla güncellendi.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Sözleşme başka bir işlemde değiştirildi. Detayı yenileyip tekrar deneyin.", StatusCode.Conflict);
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException or TimeoutException
                || ex is InvalidOperationException && ex.GetBaseException() is SqlException)
            {
                return Fail("Kayıt sonucu doğrulanamadı. Detayı yenileyip güncel bilgileri kontrol edin.", StatusCode.Conflict);
            }
            finally
            {
                if (contract is not null) db.Entry(contract).State = EntityState.Detached;
            }
        });
    }
    private static ResponseModel<CollectionContractIdentityUpdated> Fail(string message, StatusCode status = StatusCode.BadRequest) =>
        ResponseModel<CollectionContractIdentityUpdated>.Fail(message, status);
}
