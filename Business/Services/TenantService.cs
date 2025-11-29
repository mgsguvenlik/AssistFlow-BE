using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Dtos.Tenant;
using System.Linq.Expressions;

namespace Business.Services
{

    public class TenantService
          : CrudServiceBase<Tenant, long, TenantCreateDto, TenantUpdateDto, TenantGetDto>,
            ICrudService<TenantCreateDto, TenantUpdateDto, TenantGetDto, long>,
            ITenantService
    {
        public TenantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            TypeAdapterConfig config,
            IHttpContextAccessor? http = null)
            : base(unitOfWork, mapper, config, http)
        {
        }

        // Id okumak için
        protected override long ReadKey(Tenant entity) => entity.Id;

        // e => e.Id == id predicate’i
        protected override Expression<Func<Tenant, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        // Include yoksa null
        protected override Func<IQueryable<Tenant>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Tenant, object>>?
            IncludeExpression() => null;

        protected override Task<Tenant?> ResolveEntityForUpdateAsync(TenantUpdateDto dto)
        {
            return _repo.GetSingleAsync<Tenant>(
                asNoTracking: false,
                 x => x.Id == dto.Id);
        }

        public override async Task<ResponseModel<TenantGetDto>> CreateAsync(TenantCreateDto dto)
        {
            // TenantCode unique kontrolü
            var exists = await _repo.GetQueryable<Tenant>()
                .AnyAsync(x => x.Code == dto.Code);

            if (exists)
            {
                return ResponseModel<TenantGetDto>.Fail(
                    $"Bu TenantCode zaten mevcut: {dto.Code}",
                    Core.Enums.StatusCode.Conflict);
            }

            // 📁 Logo upload + LogoUrl oluştur
            dto.LogoUrl = await SaveLogoAndGetUrlAsync(dto.LogoFile, null);

            // Geri kalan işi base'e bırak
            return await base.CreateAsync(dto);
        }

        public override async Task<ResponseModel<TenantGetDto>> UpdateAsync(TenantUpdateDto dto)
        {
            // Önce mevcut entity'yi al (kod kontrolü + eski LogoUrl için)
            var entity = await _repo.GetSingleAsync<Tenant>(
                asNoTracking: false,
                 x => x.Id == dto.Id);

            if (entity == null)
                return ResponseModel<TenantGetDto>.Fail(
                    Core.Utilities.Constants.Messages.RecordNotFound,
                    Core.Enums.StatusCode.NotFound);

            // TenantCode değişmişse unique kontrolü
            if (!string.Equals(entity.Code, dto.Code, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _repo.GetQueryable<Tenant>()
                    .AnyAsync(x => x.Code == dto.Code && x.Id != dto.Id);

                if (exists)
                {
                    return ResponseModel<TenantGetDto>.Fail(
                        $"Bu TenantCode zaten mevcut: {dto.Code}",
                        Core.Enums.StatusCode.Conflict);
                }
            }

            // 📁 Yeni logo yüklendiyse kaydet, yoksa eskiyi koru
            dto.Logo = await SaveLogoAndGetUrlAsync(dto.LogoFile, entity.LogoUrl);

            // Devamını base'e bırak (MapUpdate + audit + geri dönüş)
            return await base.UpdateAsync(dto);

        }
        private async Task<string?> SaveLogoAndGetUrlAsync(IFormFile? file, string? existingUrl = null)
        {
            // Dosya yoksa mevcut URL'yi koru
            if (file == null || file.Length == 0)
                return existingUrl;

            // Kayıt klasörü: {uygulama_root}/TenantLogo
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "TenantLogo");

            // Klasör yoksa oluştur (idempotent, tekrar çağırmak sorun değil)
            Directory.CreateDirectory(folder);

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // URL tarafında kullanılacak relative path
            var relativePath = $"/TenantLogo/{fileName}";

            // AppSettings'ten baseUrl al
            var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
            var baseUrl = appSettings?.Value.FileUrl?.TrimEnd('/') ?? string.Empty;

            // FileUrl tanımlı değilse sadece relative path dön
            if (string.IsNullOrEmpty(baseUrl))
                return relativePath;

            return $"{baseUrl}{relativePath}";
        }


    }
}
