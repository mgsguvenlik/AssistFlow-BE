using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;
using System.ComponentModel.DataAnnotations;

namespace Business.Services.Crm.Collections;

/// <summary>Read service; refuses database queries until the collection model is registered.</summary>
public sealed class CollectionContractReadService(AppDataContext db, IUnitOfWork unitOfWork) : ICollectionContractReadService
{
    public async Task<ResponseModel<PagedResult<CollectionContractListItem>>> GetPageAsync(
        CollectionContractQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContract)) is null)
            return ResponseModel<PagedResult<CollectionContractListItem>>.Fail(
                "Tahsilat sözleşme modeli henüz etkin değil.", (StatusCode)503);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionContractListItem>>.Fail(
                string.Join(" ", errors.Select(x => x.ErrorMessage)), StatusCode.BadRequest);

        var source = unitOfWork.Repository.GetQueryable<CollectionContract>();
        // Sequential queries: DbContext does not support parallel operations.
        // Concurrent changes between count and page are possible; no snapshot guarantee is implied.
        var count = await CollectionContractReadQuery.Filter(source, query).CountAsync(cancellationToken);
        var items = await CollectionContractReadQuery.Page(source, query).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionContractListItem>>.Success(new(items, count, query.Page, query.PageSize), "Sözleşmeler başarıyla getirildi.");
    }

    public async Task<ResponseModel<CollectionContractDetail>> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContract)) is null)
            return ResponseModel<CollectionContractDetail>.Fail(
                "Tahsilat sözleşme modeli henüz etkin değil.", (StatusCode)503);
        if (id <= 0) return ResponseModel<CollectionContractDetail>.Fail("Geçersiz sözleşme kimliği.");
        var item = await CollectionContractReadQuery.Detail(unitOfWork.Repository.GetQueryable<CollectionContract>(), id)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? ResponseModel<CollectionContractDetail>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound)
            : ResponseModel<CollectionContractDetail>.Success(item, "Sözleşme bilgileri başarıyla getirildi.");
    }

    public async Task<ResponseModel<PagedResult<CollectionRateHistoryItem>>> GetHistoryAsync(long id,
        CollectionRateHistoryQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContractRatePeriod)) is null)
            return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Fail("Tahsilat tarife modeli henüz etkin değil.", (StatusCode)503);
        var errors = new List<ValidationResult>();
        if (id <= 0 || !Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Fail("Geçersiz sözleşme veya sayfa bilgisi.");
        var repository = unitOfWork.Repository;
        if (!await repository.GetQueryable<CollectionContract>().AsNoTracking()
            .AnyAsync(x => x.Id == id && !x.IsDeleted, cancellationToken))
            return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
        var source = repository.GetQueryable<CollectionContractRatePeriod>();
        var count = await source.CountAsync(x => x.ContractId == id && !x.IsDeleted, cancellationToken);
        var items = await CollectionContractReadQuery.History(source, id, query).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Success(new(items, count, query.Page, query.PageSize),
            "Tarife geçmişi başarıyla getirildi.");
    }
}
