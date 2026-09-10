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

    public async Task<ResponseModel<CollectionContractListItem>> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContract)) is null)
            return ResponseModel<CollectionContractListItem>.Fail(
                "Tahsilat sözleşme modeli henüz etkin değil.", (StatusCode)503);
        if (id <= 0) return ResponseModel<CollectionContractListItem>.Fail("Geçersiz sözleşme kimliği.");
        var item = await CollectionContractReadQuery.Detail(unitOfWork.Repository.GetQueryable<CollectionContract>(), id)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? ResponseModel<CollectionContractListItem>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound)
            : ResponseModel<CollectionContractListItem>.Success(item, "Sözleşme bilgileri başarıyla getirildi.");
    }
}
