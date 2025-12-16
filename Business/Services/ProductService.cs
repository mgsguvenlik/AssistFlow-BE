using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Castle.Core.Resource;
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
                     .Include(p => p.ProductType);
        protected override Task<Product?> ResolveEntityForUpdateAsync(ProductUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<Product>(false, x => x.Id == dto.Id,
                   q => q.Include(p => p.Brand)
                         .Include(p => p.Model)
                         .Include(p => p.CurrencyType)
                         .Include(p => p.ProductType));
        public async Task<ResponseModel<List<ProductEffectivePriceDto>>> GetProductsByCustomerIdAsync(long customerId)
        {
            // Müşteri + grup + fiyat ilişkileriyle birlikte yükle
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                x => x.Id == customerId,
                includeExpression: q => q
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                            .ThenInclude(gp => gp.Product)
                    .Include(x => x.CustomerProductPrices)
                        .ThenInclude(cp => cp.Product)
            );

            if (customer is null)
                return ResponseModel<List<ProductEffectivePriceDto>>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // Tüm ürünleri çek
            var products = await _unitOfWork.Repository.GetMultipleAsync<Product>(
                asNoTracking: true,
                includeExpression: q => q
                    .Include(p => p.CurrencyType)
                    .Include(p => p.Brand)
                    .Include(p => p.Model)
            );

            var result = new List<ProductEffectivePriceDto>();

            foreach (var product in products)
            {
                decimal effectivePrice = 0m;
                string? effectiveCurrency = null;

                // 1️⃣ Grup fiyatı
                var groupPrice = customer.CustomerGroup?.GroupProductPrices
                    .FirstOrDefault(gp => gp.ProductId == product.Id);

                if (groupPrice is not null)
                {
                    effectivePrice = groupPrice.Price;
                    effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
                }
                else
                {
                    // 2️⃣ Müşteri özel fiyatı
                    var customerPrice = customer.CustomerProductPrices
                        .FirstOrDefault(cp => cp.ProductId == product.Id);

                    if (customerPrice is not null)
                    {
                        effectivePrice = customerPrice.Price;
                        effectiveCurrency = customerPrice.CurrencyCode ?? product.PriceCurrency;
                    }
                    else
                    {
                        // 3️⃣ Ürün genel fiyatı
                        effectivePrice = product.Price ?? 0m;
                        effectiveCurrency = product.PriceCurrency;
                    }
                }

                result.Add(new ProductEffectivePriceDto
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    Description = product.Description,
                    BasePrice = product.Price,
                    BaseCurrency = product.PriceCurrency,
                    EffectivePrice = effectivePrice,
                    EffectiveCurrency = effectiveCurrency
                });
            }

            return ResponseModel<List<ProductEffectivePriceDto>>.Success(result);
        }
        public async Task<ResponseModel<ProductEffectivePriceDto>> GetEffectivePriceAsync(long customerId, long productId)
        {
            // 🔍 Müşteriyi ilişkili fiyatlarla birlikte getir
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                 x => x.Id == customerId,
                includeExpression: q => q
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices));

            if (customer is null)
                return ResponseModel<ProductEffectivePriceDto>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // 🔍 Ürünü getir
            var product = await _unitOfWork.Repository.GetSingleAsync<Product>(
                asNoTracking: true,
                 x => x.Id == productId);

            if (product is null)
                return ResponseModel<ProductEffectivePriceDto>.Fail("Ürün bulunamadı.", StatusCode.NotFound);

            // 💰 Fiyat hiyerarşisi
            decimal effectivePrice = 0m;
            string? effectiveCurrency = null;

            // 1️⃣ Grup fiyatı varsa
            var groupPrice = customer.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == product.Id);

            if (groupPrice is not null)
            {
                effectivePrice = groupPrice.Price;
                effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
            }
            // 2️⃣ Müşteri özel fiyatı varsa
            else if (customer.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == product.Id) is { } customerPrice)
            {
                effectivePrice = customerPrice.Price;
                effectiveCurrency = customerPrice.CurrencyCode ?? product.PriceCurrency;
            }
            // 3️⃣ Ürünün kendi fiyatı
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
                Description = product.Description,
                BasePrice = product.Price,
                BaseCurrency = product.PriceCurrency,
                EffectivePrice = effectivePrice,
                EffectiveCurrency = effectiveCurrency
            };

            return ResponseModel<ProductEffectivePriceDto>.Success(dto);
        }
        public async Task<ResponseModel<List<ProductEffectivePriceDto>>> GetEffectivePricesAsync(CustomerProductRequestDto dto)
        {
            // 🔍 Müşteriyi fiyat bilgileriyle birlikte getir
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                 x => x.Id == dto.CustomerId,
                includeExpression: q => q
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices));

            if (customer is null)
                return ResponseModel<List<ProductEffectivePriceDto>>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            // 🔍 İstenen ürünleri çek
            var products = await _unitOfWork.Repository.GetMultipleAsync<Product>(
                asNoTracking: true,
                 p => dto.ProductIds.Contains(p.Id));

            if (products is null || !products.Any())
                return ResponseModel<List<ProductEffectivePriceDto>>.Fail("Ürün bulunamadı.", StatusCode.NotFound);

            var result = new List<ProductEffectivePriceDto>();

            foreach (var product in products)
            {
                decimal effectivePrice = 0m;
                string? effectiveCurrency = null;

                // 1️⃣ Grup fiyatı
                var groupPrice = customer.CustomerGroup?.GroupProductPrices
                    .FirstOrDefault(gp => gp.ProductId == product.Id);

                if (groupPrice is not null)
                {
                    effectivePrice = groupPrice.Price;
                    effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // 2️⃣ Müşteri özel fiyatı
                else if (customer.CustomerProductPrices
                    .FirstOrDefault(cp => cp.ProductId == product.Id) is { } customerPrice)
                {
                    effectivePrice = customerPrice.Price;
                    effectiveCurrency = customerPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // 3️⃣ Ürünün kendi fiyatı
                else
                {
                    effectivePrice = product.Price ?? 0m;
                    effectiveCurrency = product.PriceCurrency;
                }

                result.Add(new ProductEffectivePriceDto
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    Description = product.Description,
                    BasePrice = product.Price,
                    BaseCurrency = product.PriceCurrency,
                    EffectivePrice = effectivePrice,
                    EffectiveCurrency = effectiveCurrency
                });
            }

            return ResponseModel<List<ProductEffectivePriceDto>>.Success(result);
        }
        public async Task<ResponseModel<PagedResult<ProductEffectivePriceDto>>> GetEffectivePriceByCustomerAsync(QueryParams q, long? customerId)
        {
            // 1️ Önce sayfalı ürün listesini al (ProductGetDto tipinde)
            var productResponse = await GetPagedAsync(q);
            if (!productResponse.IsSuccess || productResponse.Data == null)
                return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Fail("Ürün listesi alınamadı.", StatusCode.BadRequest);

            var products = productResponse.Data.Items.ToList();

            // 2️ Eğer müşteri belirtilmemişse: sadece ürün fiyatlarını döndür
            if (customerId == null)
            {
                var result = products.Select(p => new ProductEffectivePriceDto
                {
                    ProductId = p.Id,
                    ProductCode = p.ProductCode,
                    Description = p.Description,
                    BasePrice = p.Price,
                    BaseCurrency = p.PriceCurrency,
                    ProductPrice = p.Price ?? 0m,
                    EffectivePrice = p.Price ?? 0m,
                    EffectiveCurrency = p.PriceCurrency
                }).ToList();

                return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Success(
                     new PagedResult<ProductEffectivePriceDto>(result, productResponse.Data.TotalCount, q.Page, q.PageSize)
                );
            }

            // 3️ Müşteri ve ilişkili fiyat bilgilerini getir
            var customer = await _unitOfWork.Repository.GetSingleAsync<Customer>(
                asNoTracking: true,
                whereExpression: x => x.Id == customerId.Value,
                includeExpression: qx => qx
                    .Include(x => x.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                    .Include(x => x.CustomerProductPrices)
            );

            if (customer is null)
                return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Fail("Müşteri bulunamadı.", StatusCode.NotFound);

            var customerPrices = customer.CustomerProductPrices;
            var groupPrices = customer.CustomerGroup?.GroupProductPrices ?? new List<CustomerGroupProductPrice>();

            // 4️ Ürün bazında efektif fiyat hesapla
            var effectiveList = new List<ProductEffectivePriceDto>();

            foreach (var product in products)
            {
                decimal effectivePrice;
                string? effectiveCurrency;

              
                var custPrice = customerPrices.FirstOrDefault(cp => cp.ProductId == product.Id);
                 
                // Öncelik 1: Grup fiyatı
                if  (groupPrices.FirstOrDefault(gp => gp.ProductId == product.Id) is { } groupPrice)
                {
                    effectivePrice = groupPrice.Price;
                    effectiveCurrency = groupPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // Öncelik 2: Müşteri özel fiyatı
                else if  (custPrice != null)
                {
                    effectivePrice = custPrice.Price;
                    effectiveCurrency = custPrice.CurrencyCode ?? product.PriceCurrency;
                }
                // Öncelik 3️: Ürün genel fiyatı
                else
                {
                    effectivePrice = product.Price ?? 0m;
                    effectiveCurrency = product.PriceCurrency;
                }

                effectiveList.Add(new ProductEffectivePriceDto
                {
                    ProductId = product.Id,
                    ProductCode = product.ProductCode,
                    Description = product.Description,
                    BasePrice = product.Price,
                    BaseCurrency = product.PriceCurrency,
                    ProductPrice = product.Price ?? 0m,
                    EffectivePrice = effectivePrice,
                    EffectiveCurrency = effectiveCurrency
                });
            }

            return ResponseModel<PagedResult<ProductEffectivePriceDto>>.Success(
             new PagedResult<ProductEffectivePriceDto>(effectiveList, productResponse.Data.TotalCount, q.Page, q.PageSize));

        }
    }
}
