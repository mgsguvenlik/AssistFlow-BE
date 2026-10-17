using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionGroupContextService(AppDataContext db) : ICollectionGroupContextService
{
    public async Task<ResponseModel<CollectionGroupContext>> GetAsync(long groupId, CancellationToken cancellationToken = default)
    {
        var group = await db.CustomerGroups.AsNoTracking().Where(x => x.Id == groupId)
            .Select(x => new { x.Id, x.Code, x.GroupName }).SingleOrDefaultAsync(cancellationToken);
        if (group is null || CollectionCustomerClassification.Classify(group.Code) != CollectionCustomerClass.Group)
            return ResponseModel<CollectionGroupContext>.Fail("Tahsilat kapsamında grup bulunamadı.", StatusCode.NotFound);
        var parent = await (from p in db.Set<CollectionGroupParent>().AsNoTracking()
                            join c in db.Customers.AsNoTracking() on p.CustomerId equals c.Id
                            where p.CustomerGroupId == groupId && c.CustomerGroupId == groupId && !c.IsDeleted
                                && c.CustomerType != null && c.CustomerType.Code == "G"
                            select new { p.CustomerId, c.SubscriberCompany, p.AccountNo, p.IsCorporate })
                            .SingleOrDefaultAsync(cancellationToken);
        var memberTypes = CollectionCustomerClassification.GroupTypeCodes;
        var count = await db.Customers.AsNoTracking().CountAsync(x => !x.IsDeleted && x.CustomerGroupId == groupId
            && x.CustomerType != null && memberTypes.Contains(x.CustomerType.Code), cancellationToken);
        return ResponseModel<CollectionGroupContext>.Success(new(group.Id, group.Code, group.GroupName,
            parent?.CustomerId, parent?.SubscriberCompany, parent?.AccountNo, parent?.IsCorporate, count), "Grup bilgileri getirildi.");
    }

    public async Task<ResponseModel<CollectionGroupContext>> ResolveAccountAsync(string accountNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountNo) || accountNo.Trim().Length > 200)
            return ResponseModel<CollectionGroupContext>.Fail("Geçerli cari kod girin.");
        var code = accountNo.Trim();
        var ids = await db.Set<CollectionGroupParent>().AsNoTracking().Where(x => x.AccountNo == code)
            .OrderBy(x => x.CustomerGroupId).Select(x => x.CustomerGroupId).Take(2).ToListAsync(cancellationToken);
        if (ids.Count == 0) return ResponseModel<CollectionGroupContext>.Fail("Cari koda ait grup üst kartı bulunamadı.", StatusCode.NotFound);
        if (ids.Count != 1) return ResponseModel<CollectionGroupContext>.Fail("Cari kod birden fazla üst kartta kullanılıyor. Müşteri eşleştirmesi incelenmelidir.", StatusCode.Conflict);
        return await GetAsync(ids[0], cancellationToken);
    }
}
