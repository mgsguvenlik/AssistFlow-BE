using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

namespace Data.Concrete.EfCore.Collections;

/// <summary>Yalnız operasyonel kapsam. Tarihsel sözleşme/ödeme okumasını veya ortak müşteri listesini filtrelemez.</summary>
public static class CollectionCustomerScopeQuery
{
    public static IQueryable<Customer> Customers(IQueryable<Customer> source, CollectionCustomerClass? kind = null)
    {
        var individuals = kind == CollectionCustomerClass.Group ? [] : CollectionCustomerClassification.IndividualCodes.ToArray();
        var groups = kind == CollectionCustomerClass.Individual ? [] : CollectionCustomerClassification.GroupCodes.ToArray();
        var individualTypes = CollectionCustomerClassification.IndividualTypeCodes;
        var groupTypes = CollectionCustomerClassification.GroupOperationalTypeCodes;
        return source.Where(x => !x.IsDeleted && x.CustomerGroup != null && x.CustomerType != null
            && ((individuals.Contains(EF.Functions.Collate(x.CustomerGroup.Code.Trim(), "Turkish_CI_AS"))
                    && individualTypes.Contains(x.CustomerType.Code))
                || (groups.Contains(EF.Functions.Collate(x.CustomerGroup.Code.Trim(), "Turkish_CI_AS"))
                    && groupTypes.Contains(x.CustomerType.Code))));
    }

    public static IQueryable<CollectionContract> Contracts(IQueryable<CollectionContract> source,
        IQueryable<Customer> customers, CollectionCustomerClass? kind = null)
    {
        var ids = Customers(customers, kind).Select(x => x.Id);
        return source.Where(x => !x.IsDeleted && ids.Contains(x.CustomerId));
    }
}
