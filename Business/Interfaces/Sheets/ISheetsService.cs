using Core.Common;
using Model.Dtos.Sheets;

namespace Business.Interfaces.Sheets;

public interface ISheetsService
{
    Task<PagedResult<SheetListDto>> ListAsync(SheetQuery query, CancellationToken ct);
    Task<bool> CanViewAsync(Guid id, long userId, CancellationToken ct);
    Task<SheetWorkbookDto> OpenAsync(Guid id, Guid? revisionId, CancellationToken ct);
    Task<SheetWorkbookDto> CreateAsync(SheetCreateDto dto, CancellationToken ct);
    Task<SheetWorkbookDto> CreateFromExcelAsync(string name, Stream file, CancellationToken ct);
    Task<SheetWorkbookDto> SaveAsync(Guid id, SheetSaveDto dto, CancellationToken ct);
    Task<SheetWindowDto> WindowAsync(Guid id, SheetWindowQuery query, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<PagedResult<SheetUserDto>> EligibleUsersAsync(QueryParams query, CancellationToken ct);
    Task<List<SheetUserDto>> AccessAsync(Guid id, CancellationToken ct);
    Task GrantAsync(Guid id, long userId, bool grant, CancellationToken ct);
    Task<PagedResult<SheetActivityDto>> ActivitiesAsync(Guid id, QueryParams query, CancellationToken ct);
    Task<SheetWorkbookDto> ImportAsync(Guid id, SheetSaveDto draft, Stream file, CancellationToken ct);
    Task<string> ExportAsync(Guid id, Guid? revisionId, CancellationToken ct);
}
