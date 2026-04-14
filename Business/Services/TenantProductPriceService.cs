using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.TenantProductPrice;
using System.Linq.Expressions;

namespace Business.Services
{
    public class TenantProductPriceService :
      CrudServiceBase<TenantProductPrice, long,
                      TenantProductPriceCreateDto, TenantProductPriceUpdateDto, TenantProductPriceGetDto>,
      ITenantProductPriceService
    {
        public TenantProductPriceService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(TenantProductPrice e) => e.Id;

        protected override Expression<Func<TenantProductPrice, bool>> KeyPredicate(long id)
            => e => e.Id == id;

        // GET list/detail'de Tenant & Product çek
        protected override Func<IQueryable<TenantProductPrice>, IIncludableQueryable<TenantProductPrice, object>>?
            IncludeExpression()
            => q => q
                .Include(x => x.Tenant)
                .Include(x => x.Product);

        protected override async Task<TenantProductPrice?> ResolveEntityForUpdateAsync(TenantProductPriceUpdateDto dto)
            => await _unitOfWork.Repository.GetSingleAsync<TenantProductPrice>(
                    asNoTracking: false,
                    whereExpression: x => x.Id == dto.Id,
                    includeExpression: q => q.Include(x => x.Tenant)
                                             .Include(x => x.Product)
               );

        public async Task<List<TenantProductPriceGetDto>> GetByProductAndTenantAsync(long productId, long tenantId)
        {
            var entities = await _unitOfWork.Repository
                .GetQueryable<TenantProductPrice>()
                .Include(x => x.Tenant)
                .Include(x => x.Product)
                .Where(x => x.ProductId == productId && x.TenantId == tenantId)
                .ToListAsync();

            return entities.Adapt<List<TenantProductPriceGetDto>>(_config);
        }

        // 🆕 Filtreli sayfalama
        public async Task<ResponseModel<PagedResult<TenantProductPriceGetDto>>> GetPagedWithFilterAsync(TenantProductPriceQueryParams q)
        {
            try
            {
                var query = _unitOfWork.Repository.GetQueryable<TenantProductPrice>();
                var inc = IncludeExpression();
                if (inc is not null) query = inc(query);

                // 🔍 ProductId filtresi
                if (q.ProductId.HasValue)
                {
                    query = query.Where(x => x.ProductId == q.ProductId.Value);
                }

                // 🔍 TenantId filtresi
                if (q.TenantId.HasValue)
                {
                    query = query.Where(x => x.TenantId == q.TenantId.Value);
                }

                // 🔍 Search
                if (!string.IsNullOrWhiteSpace(q.Search))
                {
                    var search = q.Search.Trim();
                    query = query.Where(x =>
                        (x.Name != null && x.Name.Contains(search)) ||
                        (x.Tenant != null && x.Tenant.Name.Contains(search)) ||
                        (x.Product != null && x.Product.ProductCode != null && x.Product.ProductCode.Contains(search)) ||
                        (x.Product != null && x.Product.Description != null && x.Product.Description.Contains(search))
                    );
                }

                // 🔢 Sıralama
                if (!string.IsNullOrWhiteSpace(q.Sort))
                {
                    var prop = typeof(TenantProductPrice).GetProperty(q.Sort,
                        System.Reflection.BindingFlags.IgnoreCase |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance);

                    if (prop != null)
                    {
                        var parameter = Expression.Parameter(typeof(TenantProductPrice), "x");
                        var property = Expression.Property(parameter, prop);
                        var lambda = Expression.Lambda(property, parameter);

                        string methodName = q.Desc ? "OrderByDescending" : "OrderBy";
                        var resultExp = Expression.Call(
                            typeof(Queryable),
                            methodName,
                            new Type[] { typeof(TenantProductPrice), prop.PropertyType },
                            query.Expression,
                            Expression.Quote(lambda));

                        query = query.Provider.CreateQuery<TenantProductPrice>(resultExp);
                    }
                }
                else
                {
                    // Varsayılan sıralama: Id DESC
                    query = query.OrderByDescending(x => x.Id);
                }

                // 📄 Sayfalama
                var total = await query.CountAsync();

                var items = await query
                    .AsNoTracking()
                    .Skip((q.Page - 1) * q.PageSize)
                    .Take(q.PageSize)
                    .ProjectToType<TenantProductPriceGetDto>(_config)
                    .ToListAsync();

                return ResponseModel<PagedResult<TenantProductPriceGetDto>>.Success(
                    new PagedResult<TenantProductPriceGetDto>(items, total, q.Page, q.PageSize));
            }
            catch (Exception ex)
            {
                return ResponseModel<PagedResult<TenantProductPriceGetDto>>.Fail(
                    $"Veri getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }
    }
}