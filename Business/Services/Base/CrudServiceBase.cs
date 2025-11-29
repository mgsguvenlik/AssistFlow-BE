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
        protected virtual long? GetCurrentTenantIdOrDefault()
        {
            var user = _http?.HttpContext?.User;
            if (user is null || !user.Identity?.IsAuthenticated == true)
                return null;

            // Login'de eklediğimiz claim: "tenant_id"
            var tStr = user.FindFirstValue("tenant_id");

            if (long.TryParse(tStr, out var tid) && tid > 0)
                return tid;

            return null;
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

        public virtual async Task<ResponseModel<TGetDto>> UpdateAsync(TUpdateDto dto)
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

            // 🔹 Tenant filtresi
            query = ApplyTenantFilterIfNeeded(query);

            // 🧹 IsDeleted varsa, soft delete filtrele
            var isDeletedProp = typeof(TEntity).GetProperty(
                "IsDeleted",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (isDeletedProp != null &&
                (isDeletedProp.PropertyType == typeof(bool) || isDeletedProp.PropertyType == typeof(bool?)))
            {
                var parameter = Expression.Parameter(typeof(TEntity), "x");
                var member = Expression.Property(parameter, isDeletedProp);

                Expression body;

                if (isDeletedProp.PropertyType == typeof(bool))
                {
                    // x => x.IsDeleted == false
                    body = Expression.Equal(
                        member,
                        Expression.Constant(false, typeof(bool)));
                }
                else
                {
                    // bool? için: (x.IsDeleted ?? false) == false
                    var coalesce = Expression.Coalesce(
                        member,
                        Expression.Constant(false, typeof(bool?))
                    );
                    body = Expression.Equal(
                        coalesce,
                        Expression.Constant(false, typeof(bool?)));
                }

                var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
                query = query.Where(lambda);
            }

            // 🔍 1. Search: string + tarih + sayısal alanlarda arama
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var parameter = Expression.Parameter(typeof(TEntity), "x");

                var stringProps = typeof(TEntity).GetProperties()
                    .Where(p => p.PropertyType == typeof(string));

                var dateProps = typeof(TEntity).GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));

                var numericProps = typeof(TEntity).GetProperties()
                    .Where(p => p.PropertyType == typeof(int) ||
                                p.PropertyType == typeof(int?) ||
                                p.PropertyType == typeof(long) ||
                                p.PropertyType == typeof(long?) ||
                                p.PropertyType == typeof(decimal) ||
                                p.PropertyType == typeof(decimal?) ||
                                p.PropertyType == typeof(double) ||
                                p.PropertyType == typeof(double?) ||
                                p.PropertyType == typeof(float) ||
                                p.PropertyType == typeof(float?));

                Expression? combined = null;

                // 📄 String alanlarda arama
                foreach (var prop in stringProps)
                {
                    var member = Expression.Property(parameter, prop);
                    var notNull = Expression.NotEqual(member, Expression.Constant(null));
                    var contains = Expression.Call(
                        member,
                        nameof(string.Contains),
                        Type.EmptyTypes,
                        Expression.Constant(q.Search, typeof(string))
                    );
                    var safeContains = Expression.AndAlso(notNull, contains);
                    combined = combined == null ? safeContains : Expression.OrElse(combined, safeContains);
                }

                // 📅 DateTime arama
                if (DateTime.TryParse(q.Search, out var searchDate))
                {
                    foreach (var prop in dateProps)
                    {
                        var member = Expression.Property(parameter, prop);
                        var start = Expression.Constant(searchDate.Date);
                        var end = Expression.Constant(searchDate.Date.AddDays(1));
                        var greaterOrEqual = Expression.GreaterThanOrEqual(member, start);
                        var lessThan = Expression.LessThan(member, end);
                        var between = Expression.AndAlso(greaterOrEqual, lessThan);
                        combined = combined == null ? between : Expression.OrElse(combined, between);
                    }
                }

                // 🔢 Sayısal alanlarda arama
                if (decimal.TryParse(q.Search, out var numericValue))
                {
                    foreach (var prop in numericProps)
                    {
                        var member = Expression.Property(parameter, prop);
                        var equal = Expression.Equal(
                            member,
                            Expression.Convert(Expression.Constant(numericValue), prop.PropertyType)
                        );
                        combined = combined == null ? equal : Expression.OrElse(combined, equal);
                    }
                }

                if (combined != null)
                {
                    var lambda = Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
                    query = query.Where(lambda);
                }
            }

            // 🔢 2. Sıralama
            if (!string.IsNullOrWhiteSpace(q.Sort))
            {
                var prop = typeof(TEntity).GetProperty(q.Sort,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var parameter = Expression.Parameter(typeof(TEntity), "x");
                    var property = Expression.Property(parameter, prop);
                    var lambda = Expression.Lambda(property, parameter);

                    string methodName = q.Desc ? "OrderByDescending" : "OrderBy";
                    var resultExp = Expression.Call(
                        typeof(Queryable),
                        methodName,
                        new Type[] { typeof(TEntity), prop.PropertyType },
                        query.Expression,
                        Expression.Quote(lambda));

                    query = query.Provider.CreateQuery<TEntity>(resultExp);
                }
            }

            // 📄 3. Sayfalama
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

        /// <summary>
        /// Eğer TEntity içinde TenantId kolonu varsa ve current user'ın TenantId'si varsa,
        /// query'ye "x => x.TenantId == currentTenantId" filtresini uygular.
        /// </summary>
        protected IQueryable<TEntity> ApplyTenantFilterIfNeeded(IQueryable<TEntity> query)
        {
            // Entity'de TenantId property'si var mı?
            var tenantProp = typeof(TEntity).GetProperty(
                "TenantId",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (tenantProp == null)
                return query; // TenantId yoksa dokunma

            if (tenantProp.PropertyType != typeof(long) &&
                tenantProp.PropertyType != typeof(long?))
                return query; // Tip uymuyorsa da dokunma

            // Kullanıcının tenant'ını claim'den oku
            var currentTenantId = GetCurrentTenantIdOrDefault();
            if (!currentTenantId.HasValue)
                return query; // Kullanıcının tenant'ı yoksa filtreleme yapma

            // x => x.TenantId == currentTenantId
            var parameter = Expression.Parameter(typeof(TEntity), "x");
            var member = Expression.Property(parameter, tenantProp);

            Expression body;
            if (tenantProp.PropertyType == typeof(long))
            {
                body = Expression.Equal(
                    member,
                    Expression.Constant(currentTenantId.Value, typeof(long)));
            }
            else // long?
            {
                body = Expression.Equal(
                    member,
                    Expression.Convert(
                        Expression.Constant(currentTenantId.Value),
                        typeof(long?)));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
            return query.Where(lambda);
        }
    }
}
