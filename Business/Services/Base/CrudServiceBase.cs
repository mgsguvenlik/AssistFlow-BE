using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Utilities.Constants;
using Data.Abstract;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;

namespace Business.Services.Base
{
    public abstract class CrudServiceBase<TEntity, TKey, TCreateDto, TUpdateDto, TGetDto>
        : ICrudService<TCreateDto, TUpdateDto, TGetDto, TKey>
        where TEntity : class
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IRepository _repo;
        protected readonly IMapper _mapper;           // MapsterMapper.IMapper
        protected readonly TypeAdapterConfig _config; // Mapster config
        // ... sınıf başı alanlar:
        protected readonly IHttpContextAccessor? _http;

        protected CrudServiceBase(IUnitOfWork unitOfWork, IMapper mapper, TypeAdapterConfig config, IHttpContextAccessor? http = null)
        {
            _unitOfWork = unitOfWork;
            _repo = unitOfWork.Repository;
            _mapper = mapper;
            _config = config;
            _http = http;
        }


        // Zaman & kullanıcı id okuyucu
        protected virtual DateTimeOffset Now() => DateTimeOffset.Now;

        protected virtual long GetCurrentUserIdOrDefault()
        {
            var user = _http?.HttpContext?.User;
            if (user is null || !user.Identity?.IsAuthenticated == true) return 0;

            // En yaygın claim'ler: NameIdentifier, sub, uid
            var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue(CommonConstants.sub)
                      ?? user.FindFirstValue(CommonConstants.uid);

            return long.TryParse(idStr, out var id) ? id : 0;
        }

        // -------------------- AUDIT HELPERS --------------------
        protected virtual void SetCreateAuditIfExists(object? entity)
        {
            if (entity is null) return;
            var now = Now();
            var uid = GetCurrentUserIdOrDefault();

            TrySetDate(entity, CommonConstants.CreatedDate, now, onlyIfDefault: true);
            TrySetLong(entity, CommonConstants.CreatedUser, uid, onlyIfDefault: true);
            TrySetBool(entity, CommonConstants.IsDeleted, false, onlyIfDefault: false);
        }

        protected virtual void SetUpdateAuditIfExists(object? entity)
        {
            if (entity is null) return;
            var now = Now();
            var uid = GetCurrentUserIdOrDefault();

            TrySetDate(entity, CommonConstants.UpdatedDate, now, onlyIfDefault: false);
            TrySetLong(entity, CommonConstants.UpdatedUser, uid, onlyIfDefault: false);
        }

        protected virtual bool TrySoftDeleteIfSupported(object entity)
        {
            // IsDeleted varsa soft delete uygula
            var prop = FindProp(entity, CommonConstants.IsDeleted);
            if (prop is null) return false;

            TrySetBool(entity, CommonConstants.IsDeleted, true, onlyIfDefault: false);
            SetUpdateAuditIfExists(entity);
            return true;
        }
        private static void TrySetDate(object entity, string name, DateTimeOffset value, bool onlyIfDefault)
        {
            var p = FindProp(entity, name); if (p is null || !p.CanWrite) return;
            var t = Underlying(p);
            if (t == typeof(DateTimeOffset))
            {
                if (onlyIfDefault)
                {
                    var cur = p.GetValue(entity);
                    if (cur == null || (cur is DateTimeOffset dto && dto == default))
                        p.SetValue(entity, value);
                }
                else p.SetValue(entity, value);
            }
            else if (t == typeof(DateTime))
            {
                var val = value.UtcDateTime;
                if (onlyIfDefault)
                {
                    var cur = p.GetValue(entity);
                    if (cur == null || (cur is DateTime dt && dt == default))
                        p.SetValue(entity, val);
                }
                else p.SetValue(entity, val);
            }
        }

        private static void TrySetLong(object entity, string name, long value, bool onlyIfDefault)
        {
            var p = FindProp(entity, name); if (p is null || !p.CanWrite) return;
            var t = Underlying(p);
            if (t == typeof(long) || t == typeof(int))
            {
                if (onlyIfDefault)
                {
                    var cur = p.GetValue(entity);
                    var isDefault = cur == null ||
                                    (cur is long l && l == default) ||
                                    (cur is int i && i == default);
                    if (isDefault) p.SetValue(entity, t == typeof(long) ? value : (int)value);
                }
                else p.SetValue(entity, t == typeof(long) ? value : (int)value);
            }
        }

        private static void TrySetBool(object entity, string name, bool value, bool onlyIfDefault)
        {
            var p = FindProp(entity, name); if (p is null || !p.CanWrite) return;
            var t = Underlying(p);
            if (t == typeof(bool))
            {
                if (onlyIfDefault)
                {
                    var cur = p.GetValue(entity);
                    if (cur == null || (cur is bool b && EqualityComparer<bool>.Default.Equals(b, default)))
                        p.SetValue(entity, value);
                }
                else p.SetValue(entity, value);
            }
        }
        // -------------------- Reflection mini utils --------------------
        private static PropertyInfo? FindProp(object entity, string name) =>
            entity.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        private static Type? Underlying(PropertyInfo p)
        {
            var t = p.PropertyType;
            return Nullable.GetUnderlyingType(t) ?? t;
        }

        /// <summary> Entity'nin Id/Key'ini entity'den okuyup döndür. </summary>
        protected abstract TKey ReadKey(TEntity entity);

        /// <summary> EF tarafından çevrilebilir bir Id predicate'i ver. (e => e.Id == id) </summary>
        protected abstract Expression<Func<TEntity, bool>> KeyPredicate(TKey id);

        /// <summary> Gerekli Include'ları buraya koy. (override edilebilir) </summary>
        protected virtual Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? IncludeExpression()
            => null;

        /// <summary> Update ederken DTO'dan Entity'yi bul. (override zorunlu) </summary>
        protected abstract Task<TEntity?> ResolveEntityForUpdateAsync(TUpdateDto dto);

        /// <summary> Varsayılan update map davranışı (partial update). </summary>
        protected virtual void MapUpdate(TUpdateDto dto, TEntity entity)
            => _mapper.Map(dto, entity);

        // ----------------------------------------------
        // Ortak SAVE helper'ları (UnitOfWork üstünden)
        // ----------------------------------------------
        protected async Task<ResponseModel<TEntity>> SaveAsync<TEntityX>(TEntity entity) where TEntityX : class
        {
            var result = new ResponseModel<TEntity>();
            try
            {
                if (await _repo.CompleteAsync() > 0)
                {
                    result = new ResponseModel<TEntity>(true, StatusCode.Ok, entity);
                    result.Message = Messages.DataSavedSuccessfully;
                    return result;
                }
                result.StatusCode = StatusCode.Ok;
                result.Message = Messages.NoChangesSaved;
            }
            catch (Exception ex)
            {
                result.Message = ex.GetBaseException().Message;
            }
            return result;
        }
        protected async Task<ResponseModel> SaveAsync()
        {
            var result = new ResponseModel();
            try
            {
                if (await _repo.CompleteAsync() > 0)
                    result.IsSuccess = true;

                result.StatusCode = StatusCode.Ok;
                result.Message = Messages.DataSavedSuccessfully;
            }
            catch (Exception ex)
            {
                result.Message = ex.GetBaseException().Message;
            }
            return result;
        }

        // ----------------------------------------------
        // CRUD
        // ----------------------------------------------
        public virtual async Task<ResponseModel<TGetDto>> CreateAsync(TCreateDto dto)
        {
            try
            {
                var entity = _mapper.Map<TEntity>(dto);

                // >>> created audit
                SetCreateAuditIfExists(entity);

                await _repo.AddAsync(entity);
                await _repo.CompleteAsync();

                var id = ReadKey(entity);
                var q = _repo.GetQueryable<TEntity>();
                var inc = IncludeExpression();
                if (inc is not null) q = inc(q);

                var created = await q.AsNoTracking()
                                     .Where(KeyPredicate(id))
                                     .ProjectToType<TGetDto>(_config)
                                     .FirstAsync();

                return ResponseModel<TGetDto>.Success(created, Messages.Created, StatusCode.Created);
            }
            catch (DbUpdateException ex)
            {
                return ResponseModel<TGetDto>.Fail($"{Messages.DatabaseError}: {ex.Message}", StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<TGetDto>.Fail($"{Messages.UnexpectedError}: {ex.Message}", StatusCode.Error);
            }
        }

        public async Task<ResponseModel<TGetDto>> UpdateAsync(TUpdateDto dto)
        {
            try
            {
                var entity = await ResolveEntityForUpdateAsync(dto);
                if (entity is null)
                    return ResponseModel<TGetDto>.Fail(Messages.RecordNotFound, StatusCode.NotFound);

                MapUpdate(dto, entity);

                // >>> updated audit
                SetUpdateAuditIfExists(entity);

                await _repo.CompleteAsync();

                var id = ReadKey(entity);
                var q = _repo.GetQueryable<TEntity>();
                var inc = IncludeExpression();
                if (inc is not null) q = inc(q);

                var updated = await q.AsNoTracking()
                                     .Where(KeyPredicate(id))
                                     .ProjectToType<TGetDto>(_config)
                                     .FirstAsync();

                return ResponseModel<TGetDto>.Success(updated, Messages.Updated);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ResponseModel<TGetDto>.Fail(Messages.ConflictError, StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<TGetDto>.Fail($"{Messages.UnexpectedError}: {ex.Message}", StatusCode.Error);
            }
        }

        public async Task<ResponseModel<bool>> DeleteAsync(TKey id)
        {
            try
            {
                var entity = await _repo.GetSingleAsync<TEntity>(asNoTracking: false, KeyPredicate(id));
                if (entity is null)
                    return ResponseModel<bool>.Fail(Messages.RecordNotFound, StatusCode.NotFound, false);

                if (TrySoftDeleteIfSupported(entity))
                {
                    await _repo.CompleteAsync();
                    return ResponseModel<bool>.Success(true, Messages.Deleted, StatusCode.NoContent);
                }
                // Soft destek yoksa hard delete
                await _repo.HardDeleteAsync<TEntity>(id);
                await _repo.CompleteAsync();
                return ResponseModel<bool>.Success(true, Messages.Deleted, StatusCode.NoContent);
            }
            catch (Exception ex)
            {
                return ResponseModel<bool>.Fail($"{Messages.UnexpectedError}: {ex.Message}", StatusCode.Error, false);
            }
        }

        public virtual async Task<ResponseModel<TGetDto>> GetByIdAsync(TKey id)
        {
            var query = _repo.GetQueryable<TEntity>();
            var inc = IncludeExpression();
            if (inc is not null) query = inc(query);

            var dto = await query
                .AsNoTracking()
                .Where(KeyPredicate(id))
                .ProjectToType<TGetDto>(_config)
                .FirstOrDefaultAsync();

            return dto is null
                ? ResponseModel<TGetDto>.Fail(Messages.RecordNotFound, StatusCode.NotFound)
                : ResponseModel<TGetDto>.Success(dto);
        }

        public virtual async Task<ResponseModel<PagedResult<TGetDto>>> GetPagedAsync(QueryParams q)
        {
            var query = _repo.GetQueryable<TEntity>();
            var inc = IncludeExpression();
            if (inc is not null) query = inc(query);

            var total = await query.CountAsync();

            var items = await query
                .AsNoTracking()
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<TGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<TGetDto>>.Success(
                new PagedResult<TGetDto>(items, total, q.Page, q.PageSize));
        }

    }
}
