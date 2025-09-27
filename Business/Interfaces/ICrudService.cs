using Core.Common;

namespace Business.Interfaces
{
    public interface ICrudService<TCreateDto, TUpdateDto, TGetDto, TKey>
    {
        Task<ResponseModel<TGetDto>> CreateAsync(TCreateDto dto);
        Task<ResponseModel<TGetDto>> UpdateAsync(TUpdateDto dto);
        Task<ResponseModel<bool>> DeleteAsync(TKey id);
        Task<ResponseModel<TGetDto>> GetByIdAsync(TKey id);
        Task<ResponseModel<PagedResult<TGetDto>>> GetPagedAsync(QueryParams query);
    }
    public record QueryParams(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null, bool Desc = false);
    public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
}
