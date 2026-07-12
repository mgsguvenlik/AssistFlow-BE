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
using Model.Dtos.ProductPriceAdjustmentRule;
using System.Linq.Expressions;

namespace Business.Services
{
    public class ProductPriceAdjustmentRuleService
        : CrudServiceBase<
            ProductPriceAdjustmentRule,
            long,
            ProductPriceAdjustmentRuleCreateDto,
            ProductPriceAdjustmentRuleUpdateDto,
            ProductPriceAdjustmentRuleGetDto>,
          IProductPriceAdjustmentRuleService
    {
        public ProductPriceAdjustmentRuleService(
            IUnitOfWork uow,
            IMapper mapper,
            TypeAdapterConfig config)
            : base(uow, mapper, config)
        {
        }

        protected override long ReadKey(ProductPriceAdjustmentRule entity)
            => entity.Id;

        protected override Expression<Func<ProductPriceAdjustmentRule, bool>>
            KeyPredicate(long id)
            => x => x.Id == id;

        protected override Func<
            IQueryable<ProductPriceAdjustmentRule>,
            IIncludableQueryable<ProductPriceAdjustmentRule, object>>?
            IncludeExpression()
            => query => query
                .Include(x => x.Product)
                .Include(x => x.Tenant);

        protected override Task<ProductPriceAdjustmentRule?>
            ResolveEntityForUpdateAsync(ProductPriceAdjustmentRuleUpdateDto dto)
            => _unitOfWork.Repository
                .GetSingleAsync<ProductPriceAdjustmentRule>(
                    asNoTracking: false,
                    whereExpression: x => x.Id == dto.Id,
                    includeExpression: query => query
                        .Include(x => x.Product)
                        .Include(x => x.Tenant));

        public async Task<ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>>GetByTenantIdAsync(long tenantId)
        {
            if (tenantId <= 0)
            {
                return ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>.Fail(
                    "Geçerli bir tenant Id değeri gönderilmelidir.",
                    StatusCode.BadRequest);
            }

            var tenantExists = await _unitOfWork.Repository
                .GetQueryable<Tenant>()
                .AsNoTracking()
                .AnyAsync(x => x.Id == tenantId);

            if (!tenantExists)
            {
                return ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>.Fail(
                    "Tenant bulunamadı.",
                    StatusCode.NotFound);
            }

            var rules = await _unitOfWork.Repository
                .GetQueryable<ProductPriceAdjustmentRule>()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Product.Description)
                .ThenBy(x => x.Name)
                .ProjectToType<ProductPriceAdjustmentRuleGetDto>(_config)
                .ToListAsync();

            return ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>
                .Success(rules);
        }

        public async Task<ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>> GetActiveByTenantAndProductAsync(long tenantId, long productId)
        {
            if (tenantId <= 0 || productId <= 0)
            {
                return ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>.Fail(
                    "tenantId ve productId zorunludur.",
                    StatusCode.BadRequest);
            }

            var rules = await _unitOfWork.Repository
                .GetQueryable<ProductPriceAdjustmentRule>()
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ProductId == productId &&
                    x.IsActive)
                .OrderBy(x => x.Name)
                .ProjectToType<ProductPriceAdjustmentRuleGetDto>(_config)
                .ToListAsync();

            return ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>
                .Success(rules);
        }

        private async Task<string?> ValidateRuleAsync(long tenantId,long productId, string code,long? ignoredId = null)
        {
            var tenantExists = await _unitOfWork.Repository
                .GetQueryable<Tenant>()
                .AsNoTracking()
                .AnyAsync(x => x.Id == tenantId);

            if (!tenantExists)
                return "Tenant bulunamadı.";

            var productExists = await _unitOfWork.Repository
                .GetQueryable<Product>()
                .AsNoTracking()
                .AnyAsync(x => x.Id == productId);

            if (!productExists)
                return "Ürün bulunamadı.";

            var normalizedCode = code.Trim().ToUpperInvariant();

            var duplicateExists = await _unitOfWork.Repository
                .GetQueryable<ProductPriceAdjustmentRule>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.TenantId == tenantId &&
                    x.ProductId == productId &&
                    x.Code == normalizedCode &&
                    (!ignoredId.HasValue || x.Id != ignoredId.Value));

            if (duplicateExists)
            {
                return "Aynı tenant, ürün ve kural kodu için daha önce bir kayıt oluşturulmuş.";
            }

            return null;
        }

        /*
         * Aşağıdaki override'lar için CrudServiceBase içindeki
         * CreateAsync ve UpdateAsync metotlarının virtual olması gerekir.
         *
         * Mevcut base sınıfınızda virtual değilse aynı kontrolleri
         * CrudServiceBase'in BeforeCreate / BeforeUpdate benzeri hook'larına
         * taşımanız gerekir.
         */

        public override async Task<ResponseModel<ProductPriceAdjustmentRuleGetDto>>CreateAsync(ProductPriceAdjustmentRuleCreateDto dto)
        {
            dto.Code = dto.Code.Trim().ToUpperInvariant();
            dto.Name = dto.Name.Trim();
            dto.Description = dto.Description?.Trim();

            var validationError = await ValidateRuleAsync(
                dto.TenantId,
                dto.ProductId,
                dto.Code);

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return ResponseModel<ProductPriceAdjustmentRuleGetDto>.Fail(
                    validationError,
                    StatusCode.BadRequest);
            }

            return await base.CreateAsync(dto);
        }

        public override async Task<ResponseModel<ProductPriceAdjustmentRuleGetDto>>UpdateAsync(ProductPriceAdjustmentRuleUpdateDto dto)
        {
            var existing = await _unitOfWork.Repository
                .GetQueryable<ProductPriceAdjustmentRule>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

            if (existing is null)
            {
                return ResponseModel<ProductPriceAdjustmentRuleGetDto>.Fail(
                    "Fiyat düzenleme kuralı bulunamadı.",
                    StatusCode.NotFound);
            }

            dto.Code = dto.Code.Trim().ToUpperInvariant();
            dto.Name = dto.Name.Trim();
            dto.Description = dto.Description?.Trim();

            var validationError = await ValidateRuleAsync(
                dto.TenantId,
                dto.ProductId,
                dto.Code,
                dto.Id);

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return ResponseModel<ProductPriceAdjustmentRuleGetDto>.Fail(
                    validationError,
                    StatusCode.BadRequest);
            }

            return await base.UpdateAsync(dto);
        }
    }
}