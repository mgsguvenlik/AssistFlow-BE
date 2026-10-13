using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Business.Utilities;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Product;
using System.Linq.Expressions;

namespace Business.Services
{
    public class ProductService
      : CrudServiceBase<Product, long, ProductCreateDto, ProductUpdateDto, ProductGetDto>,
        IProductService
    {
        public ProductService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(Product e) => e.Id;

        protected override Expression<Func<Product, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<Product>, IIncludableQueryable<Product, object>>? IncludeExpression()
            => q => q.Include(p => p.Brand)
                     .Include(p => p.Model)
                     .Include(p => p.CurrencyType)
                     .Include(p => p.CustomerProductPrices)
                     .Include(p => p.ProductType)
                     .Include(p => p.TenantProductPrices);

        protected override Task<Product?> ResolveEntityForUpdateAsync(ProductUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<Product>(false, x => x.Id == dto.Id,
                   q => q.Include(p => p.Brand)
                         .Include(p => p.Model)
                         .Include(p => p.CurrencyType)
                         .Include(p => p.ProductType));

        private IQueryable<Product> SearchProducts(IQueryable<Product> query, string? search, long[]? effectivePriceMatches = null, bool includeInternalIdentifiers = false)
            => ProductCatalogSearch.Apply(query, _repo.GetQueryable<SystemType>(), search, effectivePriceMatches, includeInternalIdentifiers);

        private static IQueryable<Product> SortProducts(IQueryable<Product> query, QueryParams q)
        {
            var prop = typeof(Product).GetProperty(q.Sort ?? "",
                System.Reflection.BindingFlags.IgnoreCase |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            if (prop == null) return query.OrderByDescending(x => x.Id);
            var parameter = Expression.Parameter(typeof(Product), "x");
            var lambda = Expression.Lambda(Expression.Property(parameter, prop), parameter);
            var expression = Expression.Call(typeof(Queryable), q.Desc ? "OrderByDescending" : "OrderBy",
                new[] { typeof(Product), prop.PropertyType }, query.Expression, Expression.Quote(lambda));
            return query.Provider.CreateQuery<Product>(expression);
        }

        public override async Task<ResponseModel<PagedResult<ProductGetDto>>> GetPagedAsync(QueryParams q)
        {
            var query = _repo.GetQueryable<Product>();
            var include = IncludeExpression();
            if (include != null) query = include(query);
            query = SortProducts(SearchProducts(ApplyTenantFilterIfNeeded(query), q.Search, includeInternalIdentifiers: true), q);
            var total = await query.CountAsync();
            var items = await query.AsNoTracking().Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .ProjectToType<ProductGetDto>(_config).ToListAsync();
            return ResponseModel<PagedResult<ProductGetDto>>.Success(new(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<List<ProductEffectivePriceDto>>> GetProductsByCustomerIdAsync(long customerId)
        {
            // 🔍 Müşteri + grup + tenant + fiyat ilişkileriyle birlikte yükle
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                whereExpression: x => x.Id == customerId,
                includeExpression: q => q
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices)
                    .Include(x => x.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
            );

            if (customer is null)
                return ResponseModel<List<ProductEffectivePriceDto>>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // 🆕 Tenant'a ait ürünleri filtrele
            IEnumerable<Product> products;

            if (customer.TenantId.HasValue)
            {
                // Sadece bu Tenant için fiyatlandırılmış ürün ID'lerini getir
                var tenantPrices = await _unitOfWork.Repository.GetMultipleAsync<TenantProductPrice>(
                    asNoTracking: true,
                    whereExpression: tp => tp.TenantId == customer.TenantId.Value
                );

                if (tenantPrices == null || !tenantPrices.Any())
                    return ResponseModel<List<ProductEffectivePriceDto>>.Success(new List<ProductEffectivePriceDto>());

                var tenantProductIds = tenantPrices.Select(tp => tp.ProductId).ToList();

                products = await _unitOfWork.Repository.GetMultipleAsync<Product>(
                    asNoTracking: true,
                    whereExpression: p => tenantProductIds.Contains(p.Id),
                    includeExpression: q => q
                        .Include(p => p.CurrencyType)
                        .Include(p => p.Brand)
                        .Include(p => p.Model)
                );
            }
            else
            {
                // Tenant yoksa tüm ürünleri getir
                products = await _unitOfWork.Repository.GetMultipleAsync<Product>(
                    asNoTracking: true,
                    includeExpression: q => q
                        .Include(p => p.CurrencyType)
                        .Include(p => p.Brand)
                        .Include(p => p.Model)
                );
            }

            var result = new List<ProductEffectivePriceDto>();
            var tenantPricesList = customer.Tenant?.TenantProductPrices?.ToList() ?? new List<TenantProductPrice>();

            foreach (var product in products)
            {
                decimal effectivePrice = 0m;
                string? effectiveCurrency = null;

                // 1️⃣ Grup fiyatı
                var groupPrice = customer.CustomerGroup?.GroupProductPrices
                    ?.FirstOrDefault(gp => gp.ProductId == product.Id);

                if (groupPrice is not null)
                {
                    effectivePrice = groupPrice.Price;
                    effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
                }
                else
                {
                    // 2️⃣ Müşteri özel fiyatı
                    var customerPrice = customer.CustomerProductPrices
                        ?.FirstOrDefault(cp => cp.ProductId == product.Id);

                    if (customerPrice is not null)
                    {
                        effectivePrice = customerPrice.Price;
                        effectiveCurrency = customerPrice.CurrencyCode ?? product.PriceCurrency;
                    }
                    else
                    {
                        // 3️⃣ Tenant fiyatı 🆕
                        var tenantPrice = tenantPricesList.FirstOrDefault(tp => tp.ProductId == product.Id);

                        if (tenantPrice is not null)
                        {
                            effectivePrice = tenantPrice.Price;
                            effectiveCurrency = tenantPrice.CurrencyCode ?? product.PriceCurrency;
                        }
                        else
                        {
                            // 4️⃣ Ürün genel fiyatı
                            effectivePrice = product.Price ?? 0m;
                            effectiveCurrency = product.PriceCurrency;
                        }
                    }
                }

                result.Add(new ProductEffectivePriceDto
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    SystemType = product.SystemType,
                    Description = product.Description,
                    BasePrice = product.Price,
                    BaseCurrency = product.PriceCurrency,
                    ProductPrice = product.Price ?? 0m,
                    EffectivePrice = effectivePrice,
                    EffectiveCurrency = effectiveCurrency,
                    IsServiceFeeProduct = product.IsServiceFeeProduct,
                    ServiceFeePercentage = product.ServiceFeePercentage

                });
            }

            return ResponseModel<List<ProductEffectivePriceDto>>.Success(result);
        }

        public async Task<ResponseModel<ProductEffectivePriceDto>> GetEffectivePriceAsync(long customerId, long productId)
        {
            // 🔍 Müşteriyi ilişkili fiyatlarla birlikte getir
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                whereExpression: x => x.Id == customerId,
                includeExpression: q => q
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices)
                    .Include(x => x.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
            );

            if (customer is null)
                return ResponseModel<ProductEffectivePriceDto>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // 🆕 Tenant kontrolü - Ürün bu tenant'a ait mi?
            if (customer.TenantId.HasValue)
            {
                var tenantProduct = await _unitOfWork.Repository.GetSingleAsync<TenantProductPrice>(
                    asNoTracking: true,
                    whereExpression: tp => tp.TenantId == customer.TenantId.Value && tp.ProductId == productId
                );

                if (tenantProduct is null)
                    return ResponseModel<ProductEffectivePriceDto>.Fail("Bu ürün müşterinin tenant'ına ait değil.", StatusCode.BadRequest);
            }

            // 🔍 Ürünü getir
            var product = await _unitOfWork.Repository.GetSingleAsync<Product>(
                asNoTracking: true,
                whereExpression: x => x.Id == productId
            );

            if (product is null)
                return ResponseModel<ProductEffectivePriceDto>.Fail("Ürün bulunamadı.", StatusCode.NotFound);

            // 💰 Fiyat hiyerarşisi
            decimal effectivePrice = 0m;
            string? effectiveCurrency = null;

            // 1️⃣ Grup fiyatı varsa
            var groupPrice = customer.CustomerGroup?.GroupProductPrices
                ?.FirstOrDefault(gp => gp.ProductId == product.Id);

            if (groupPrice is not null)
            {
                effectivePrice = groupPrice.Price;
                effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
            }
            // 2️⃣ Müşteri özel fiyatı varsa
            else if (customer.CustomerProductPrices
                ?.FirstOrDefault(cp => cp.ProductId == product.Id) is { } customerPrice)
            {
                effectivePrice = customerPrice.Price;
                effectiveCurrency = customerPrice.CurrencyCode ?? product.PriceCurrency;
            }
            // 3️⃣ Tenant fiyatı 🆕
            else if (customer.Tenant?.TenantProductPrices
                ?.FirstOrDefault(tp => tp.ProductId == product.Id) is { } tenantPrice)
            {
                effectivePrice = tenantPrice.Price;
                effectiveCurrency = tenantPrice.CurrencyCode ?? product.PriceCurrency;
            }
            // 4️⃣ Ürünün kendi fiyatı
            else
            {
                effectivePrice = product.Price ?? 0m;
                effectiveCurrency = product.PriceCurrency;
            }

            // 🔁 DTO oluştur
            var dto = new ProductEffectivePriceDto
            {
                ProductId = product.Id,
                ProductCode = product.ProductCode,
                SystemType = product.SystemType,
                Description = product.Description,
                BasePrice = product.Price,
                BaseCurrency = product.PriceCurrency,
                ProductPrice = product.Price ?? 0m,
                EffectivePrice = effectivePrice,
                EffectiveCurrency = effectiveCurrency,
                IsServiceFeeProduct = product.IsServiceFeeProduct,
                ServiceFeePercentage = product.ServiceFeePercentage
            };

            return ResponseModel<ProductEffectivePriceDto>.Success(dto);
        }

        public async Task<ResponseModel<List<ProductEffectivePriceDto>>> GetEffectivePricesAsync(CustomerProductRequestDto dto)
        {
            // 🔍 Müşteriyi fiyat bilgileriyle birlikte getir
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                whereExpression: x => x.Id == dto.CustomerId,
                includeExpression: q => q
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices)
                    .Include(x => x.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
            );

            if (customer is null)
                return ResponseModel<List<ProductEffectivePriceDto>>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // 🆕 Tenant kontrolü - İstenen ürünler tenant'a ait mi?
            if (customer.TenantId.HasValue)
            {
                var tenantPricesForProducts = await _unitOfWork.Repository.GetMultipleAsync<TenantProductPrice>(
                    asNoTracking: true,
                    whereExpression: tp => tp.TenantId == customer.TenantId.Value && dto.ProductIds.Contains(tp.ProductId)
                );

                var tenantProductIds = tenantPricesForProducts?.Select(tp => tp.ProductId).ToList() ?? new List<long>();
                var invalidProductIds = dto.ProductIds.Except(tenantProductIds).ToList();

                if (invalidProductIds.Any())
                    return ResponseModel<List<ProductEffectivePriceDto>>.Fail(
                        $"Şu ürünler müşterinin tenant'ına ait değil: {string.Join(", ", invalidProductIds)}",
                        StatusCode.BadRequest);
            }

            // 🔍 İstenen ürünleri çek
            var products = await _unitOfWork.Repository.GetMultipleAsync<Product>(
                asNoTracking: true,
                whereExpression: p => dto.ProductIds.Contains(p.Id)
            );

            if (products is null || !products.Any())
                return ResponseModel<List<ProductEffectivePriceDto>>.Fail("Ürün bulunamadı.", StatusCode.NotFound);

            var result = new List<ProductEffectivePriceDto>();
            var tenantPricesList = customer.Tenant?.TenantProductPrices?.ToList() ?? new List<TenantProductPrice>();

            foreach (var product in products)
            {
                decimal effectivePrice = 0m;
                string? effectiveCurrency = null;

                // 1️⃣ Grup fiyatı
                var groupPrice = customer.CustomerGroup?.GroupProductPrices
                    ?.FirstOrDefault(gp => gp.ProductId == product.Id);

                if (groupPrice is not null)
                {
                    effectivePrice = groupPrice.Price;
                    effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // 2️⃣ Müşteri özel fiyatı
                else if (customer.CustomerProductPrices
                    ?.FirstOrDefault(cp => cp.ProductId == product.Id) is { } customerPrice)
                {
                    effectivePrice = customerPrice.Price;
                    effectiveCurrency = customerPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // 3️⃣ Tenant fiyatı 🆕
                else if (tenantPricesList.FirstOrDefault(tp => tp.ProductId == product.Id) is { } tenantPrice)
                {
                    effectivePrice = tenantPrice.Price;
                    effectiveCurrency = tenantPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // 4️⃣ Ürünün kendi fiyatı
                else
                {
                    effectivePrice = product.Price ?? 0m;
                    effectiveCurrency = product.PriceCurrency;
                }

                result.Add(new ProductEffectivePriceDto
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    SystemType = product.SystemType,
                    Description = product.Description,
                    BasePrice = product.Price,
                    BaseCurrency = product.PriceCurrency,
                    ProductPrice = product.Price ?? 0m,
                    EffectivePrice = effectivePrice,
                    EffectiveCurrency = effectiveCurrency,
                    IsServiceFeeProduct = product.IsServiceFeeProduct,
                    ServiceFeePercentage = product.ServiceFeePercentage
                });
            }

            return ResponseModel<List<ProductEffectivePriceDto>>.Success(result);
        }

        /// <summary>
        /// 🎯 Müşteri bazlı ürün listesi + efektif fiyat hesaplama (TENANT FİLTRELİ)
        /// </summary>
        public async Task<ResponseModel<PagedResult<ProductEffectivePriceDto>>> GetEffectivePriceByCustomerAsync(QueryParams q, long? customerId)
        {
            // 1️⃣ Müşteri belirtilmemişse: sadece ürün fiyatlarını döndür (tenant filtresi YOK)
            if (customerId == null)
            {
                var defaultProductResponse = await GetPagedAsync(q);
                if (!defaultProductResponse.IsSuccess || defaultProductResponse.Data == null)
                    return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Fail("Ürün listesi alınamadı.", StatusCode.BadRequest);

                var defaultProducts = defaultProductResponse.Data.Items.ToList();
                var defaultResult = defaultProducts.Select(p => new ProductEffectivePriceDto
                {
                    ProductId = p.Id,
                    ProductCode = p.ProductCode,
                    SystemType = p.SystemType,
                    Description = p.Description,
                    BasePrice = p.Price,
                    BaseCurrency = p.PriceCurrency,
                    ProductPrice = p.Price ?? 0m,
                    EffectivePrice = p.Price ?? 0m,
                    EffectiveCurrency = p.PriceCurrency,
                    IsServiceFeeProduct = p.IsServiceFeeProduct,
                    ServiceFeePercentage = p.ServiceFeePercentage

                }).ToList();

                return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Success(
                     new PagedResult<ProductEffectivePriceDto>(defaultResult, defaultProductResponse.Data.TotalCount, q.Page, q.PageSize)
                );
            }

            // 2️⃣ Müşteri ve ilişkili fiyat bilgilerini getir
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                whereExpression: x => x.Id == customerId.Value,
                includeExpression: qx => qx
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices)
                    .Include(x => x.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
            );

            if (customer is null)
                return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // 3️⃣ Tenant'a ait ürün ID'lerini al
            List<long>? tenantProductIds = null;
            if (customer.TenantId.HasValue)
            {
                var tenantPrices = await _unitOfWork.Repository.GetMultipleAsync<TenantProductPrice>(
                    asNoTracking: true,
                    whereExpression: tp => tp.TenantId == customer.TenantId.Value
                );

                tenantProductIds = tenantPrices?.Select(tp => tp.ProductId).ToList();

                if (tenantProductIds == null || !tenantProductIds.Any())
                    return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Success(
                        new PagedResult<ProductEffectivePriceDto>(new List<ProductEffectivePriceDto>(), 0, q.Page, q.PageSize)
                    );
            }

            // 4️⃣ Ürünleri getir - TENANT FİLTRESİ İLE 🆕
            var query = _unitOfWork.Repository.GetQueryable<Product>();
            var inc = IncludeExpression();
            if (inc is not null) query = inc(query);

            // 🔍 Tenant filtresi uygula (varsa)
            if (tenantProductIds != null)
            {
                query = query.Where(p => tenantProductIds.Contains(p.Id));
            }

            var priceOverrides = (customer.CustomerGroup?.GroupProductPrices ?? new List<CustomerGroupProductPrice>())
                .Select(x => (x.ProductId, x.Price, Currency: x.CurrencyCode))
                .Concat(customer.CustomerProductPrices.Select(x => (x.ProductId, x.Price, Currency: x.CurrencyCode)))
                .Concat((customer.Tenant?.TenantProductPrices ?? new List<TenantProductPrice>())
                    .Select(x => (x.ProductId, x.Price, Currency: x.CurrencyCode)));
            var priceMatches = ProductCatalogSearch.EffectivePriceMatches(priceOverrides, q.Search);
            query = SortProducts(SearchProducts(query, q.Search, priceMatches), q);

            // 📄 Sayfalama
            var totalCount = await query.CountAsync();

            var filteredProducts = await query
                .AsNoTracking()
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<ProductGetDto>(_config)
                .ToListAsync();

            var customerPrices = customer.CustomerProductPrices;
            var groupPrices = customer.CustomerGroup?.GroupProductPrices ?? new List<CustomerGroupProductPrice>();
            var tenantPricesList = customer.Tenant?.TenantProductPrices?.ToList() ?? new List<TenantProductPrice>();

            // 5️⃣ Ürün bazında efektif fiyat hesapla
            var effectiveList = new List<ProductEffectivePriceDto>();

            foreach (var product in filteredProducts)
            {
                decimal effectivePrice;
                string? effectiveCurrency;

                // Öncelik 1: Grup fiyatı
                if (groupPrices.FirstOrDefault(gp => gp.ProductId == product.Id) is { } groupPrice)
                {
                    effectivePrice = groupPrice.Price;
                    effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // Öncelik 2: Müşteri özel fiyatı
                else if (customerPrices?.FirstOrDefault(cp => cp.ProductId == product.Id) is { } custPrice)
                {
                    effectivePrice = custPrice.Price;
                    effectiveCurrency = custPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // Öncelik 3: Tenant fiyatı 🆕
                else if (tenantPricesList.FirstOrDefault(tp => tp.ProductId == product.Id) is { } tenantPrice)
                {
                    effectivePrice = tenantPrice.Price;
                    effectiveCurrency = tenantPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // Öncelik 4: Ürün genel fiyatı
                else
                {
                    effectivePrice = product.Price ?? 0m;
                    effectiveCurrency = product.PriceCurrency;
                }

                effectiveList.Add(new ProductEffectivePriceDto
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    SystemType = product.SystemType,
                    Description = product.Description,
                    BasePrice = product.Price,
                    BaseCurrency = product.PriceCurrency,
                    ProductPrice = product.Price ?? 0m,
                    EffectivePrice = effectivePrice,
                    EffectiveCurrency = effectiveCurrency,
                    IsServiceFeeProduct = product.IsServiceFeeProduct,
                    ServiceFeePercentage = product.ServiceFeePercentage
                });
            }

            return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Success(
             new PagedResult<ProductEffectivePriceDto>(effectiveList, totalCount, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<PagedResult<ProductGetDto>>> GetPurchaseProductsAsync(QueryParams q, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.Repository.GetQueryable<Product>();

            var inc = IncludeExpression();

            if (inc is not null)
            {
                query = inc(query);
            }

            // Mevcut tenant kuralını koru.
            query = ApplyTenantFilterIfNeeded(query);

            // Satın Alma ekranında sadece Ürün tipi.
            query = query.Where(x => !x.IsDeleted && x.ProductTypeId == 1);

            query = SortProducts(SearchProducts(query, q.Search), q);

            var totalCount =
                await query.CountAsync(cancellationToken);

            var items = await query
                .AsNoTracking()
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<ProductGetDto>(_config)
                .ToListAsync(cancellationToken);

            return ResponseModel<PagedResult<ProductGetDto>>.Success(
                new PagedResult<ProductGetDto>(
                    items,
                    totalCount,
                    q.Page,
                    q.PageSize));
        }
    }
}
