using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Data.Abstract;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

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

        protected CrudServiceBase(IUnitOfWork unitOfWork, IMapper mapper, TypeAdapterConfig config)
        {
            _unitOfWork = unitOfWork;
            _repo = unitOfWork.Repository;
            _mapper = mapper;
            _config = config;
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
                    result.Message = "Save operation completed successfully.";
                    return result;
                }
                result.StatusCode = StatusCode.Ok;
                result.Message = "No changes were persisted.";
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
                result.Message = "Save operation completed successfully.";
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
                await _repo.AddAsync(entity);
                await _repo.CompleteAsync();

                var id = ReadKey(entity);

                // Include gerekiyorsa IncludeExpression ile projekte et
                var query = _repo.GetQueryable<TEntity>();
                var inc = IncludeExpression();
                if (inc is not null) query = inc(query);

                var created = await query
                    .AsNoTracking()
                    .Where(KeyPredicate(id))
                    .ProjectToType<TGetDto>(_config)
                    .FirstAsync();

                return ResponseModel<TGetDto>.Success(created, "Created", StatusCode.Created);
            }
            catch (DbUpdateException ex)
            {
                return ResponseModel<TGetDto>.Fail($"DB error: {ex.Message}", StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<TGetDto>.Fail($"Unexpected error: {ex.Message}", StatusCode.Error);
            }
        }

        public virtual async Task<ResponseModel<TGetDto>> UpdateAsync(TUpdateDto dto)
        {
            try
            {
                var entity = await ResolveEntityForUpdateAsync(dto);
                if (entity is null)
                    return ResponseModel<TGetDto>.Fail("Not found", StatusCode.NotFound);

                MapUpdate(dto, entity);

                await _repo.CompleteAsync();

                // Güncelleneni include'larla tekrar çekmek istersen:
                var id = ReadKey(entity);
                var query = _repo.GetQueryable<TEntity>();
                var inc = IncludeExpression();
                if (inc is not null) query = inc(query);

                var updated = await query
                    .AsNoTracking()
                    .Where(KeyPredicate(id))
                    .ProjectToType<TGetDto>(_config)
                    .FirstAsync();

                return ResponseModel<TGetDto>.Success(updated, "Updated");
            }
            catch (DbUpdateConcurrencyException)
            {
                return ResponseModel<TGetDto>.Fail("Concurrency conflict", StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<TGetDto>.Fail($"Unexpected error: {ex.Message}", StatusCode.Error);
            }
        }

        public virtual async Task<ResponseModel<bool>> DeleteAsync(TKey id)
        {
            try
            {
                // Varlık var mı?
                var exists = await _repo.GetSingleAsync<TEntity>(asNoTracking: true, KeyPredicate(id));
                if (exists is null)
                    return ResponseModel<bool>.Fail("Not found", StatusCode.NotFound, false);

                await _repo.HardDeleteAsync<TEntity>(id);
                await _repo.CompleteAsync();

                return ResponseModel<bool>.Success(true, "Deleted", StatusCode.NoContent);
            }
            catch (Exception ex)
            {
                return ResponseModel<bool>.Fail($"Unexpected error: {ex.Message}", StatusCode.Error, false);
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
                ? ResponseModel<TGetDto>.Fail("Not found", StatusCode.NotFound)
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
