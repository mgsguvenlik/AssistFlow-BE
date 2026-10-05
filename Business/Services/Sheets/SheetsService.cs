using System.Data;
using Business.Interfaces;
using Business.Interfaces.Sheets;
using Business.UnitOfWork;
using Core.Common;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Sheets;
using Model.Dtos.Sheets;
using static Business.Services.Sheets.SheetSnapshotEngine;

namespace Business.Services.Sheets;

public sealed partial class SheetsService(IUnitOfWork uow, AppDataContext db, ICurrentUser current) : ISheetsService
{
    private IQueryable<T> Query<T>() where T : class => uow.Repository.GetQueryable<T>();
    private async Task<(long Id, bool View, bool Edit, bool Manage, bool Create)> Permissions(CancellationToken ct)
    {
        await current.GetAsync(ct);
        if (current.Id <= 0 || !await Query<User>().AnyAsync(x => x.Id == current.Id && x.IsActive && !x.IsDeleted, ct))
            throw new SheetRuleException("Kayıtlı ve aktif kullanıcı gerekli.", 403);
        var grants = await (from ur in Query<UserRole>()
            join role in Query<Role>() on ur.RoleId equals role.Id
            join mr in Query<MenuRole>() on ur.RoleId equals mr.RoleId
            join menu in Query<Menu>() on mr.MenuId equals menu.Id
            where ur.UserId == current.Id && !role.IsDeleted && new[] { "SheetsList", "SheetsCreate", "SheetsManage" }.Contains(menu.Name)
            select new { menu.Name, mr.HasView, mr.HasEdit }).ToListAsync(ct);
        bool Has(string key, bool edit) => grants.Any(x => x.Name == key && (edit ? x.HasEdit : x.HasView));
        var manager = Has("SheetsManage", true);
        return (current.Id, Has("SheetsList", false) || Has("SheetsManage", false) || manager,
            Has("SheetsList", true) || manager, manager, Has("SheetsCreate", true) || manager);
    }
    private IQueryable<SheetWorkbook> Visible(long userId, bool all) =>
        Query<SheetWorkbook>().Where(x => all || x.OwnerId == userId ||
            Query<SheetAccess>().Any(a => a.WorkbookId == x.Id && a.UserId == userId));
    public async Task<bool> CanViewAsync(Guid id, long userId, CancellationToken ct)
    {
        if (!await Query<User>().AnyAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, ct)) return false;
        var grants = await (from ur in Query<UserRole>()
            join role in Query<Role>() on ur.RoleId equals role.Id
            join mr in Query<MenuRole>() on ur.RoleId equals mr.RoleId
            join menu in Query<Menu>() on mr.MenuId equals menu.Id
            where ur.UserId == userId && !role.IsDeleted && new[] { "SheetsList", "SheetsManage" }.Contains(menu.Name)
            select new { menu.Name, mr.HasView, mr.HasEdit }).ToListAsync(ct);
        var all = grants.Any(x => x.Name == "SheetsManage" && (x.HasView || x.HasEdit));
        return (all || grants.Any(x => x.Name == "SheetsList" && x.HasView)) &&
            await Visible(userId, all).AnyAsync(x => x.Id == id, ct);
    }
    private async Task<SheetWorkbook> Require(Guid id, string action, CancellationToken ct)
    {
        var p = await Permissions(ct);
        var all = p.Manage;
        // View-only management menu also grants visibility of all workbooks.
        if (!all)
            all = await (from ur in Query<UserRole>() join mr in Query<MenuRole>() on ur.RoleId equals mr.RoleId
                join role in Query<Role>() on ur.RoleId equals role.Id
                join menu in Query<Menu>() on mr.MenuId equals menu.Id
                where ur.UserId == p.Id && !role.IsDeleted && menu.Name == "SheetsManage" && mr.HasView
                select mr.Id).AnyAsync(ct);
        if (!p.View) throw new SheetRuleException("MGS Tablolar görüntüleme yetkisi gerekli.", 403);
        var book = await Visible(p.Id, all).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new SheetRuleException("Tablo bulunamadı veya erişiminiz yok.", 404);
        if (action == "edit" && !p.Edit) throw new SheetRuleException("Düzenleme yetkisi gerekli.", 403);
        if (action == "manage" && !p.Manage && book.OwnerId != p.Id) throw new SheetRuleException("Tabloyu yalnızca sahibi veya yönetici yönetebilir.", 403);
        return book;
    }
    private async Task<SheetRevision> Revision(Guid id, Guid revisionId, CancellationToken ct) =>
        await Query<SheetRevision>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkbookId == id && x.Id == revisionId &&
            (!x.IsDraft || x.CreatedBy == current.Id), ct) ?? throw new SheetRuleException("Tablo sürümü bulunamadı.", 404);
    private async Task<SheetWorkbookDto> Dto(SheetWorkbook book, SheetRevision revision, CancellationToken ct)
    {
        var p = await Permissions(ct);
        var snapshot = Deserialize<WorkbookSnapshot>(revision.SnapshotJson);
        return new(book.Id, book.Name, revision.Id, book.OwnerId, p.Edit, p.Manage || book.OwnerId == p.Id,
            snapshot.Worksheets.Select(ToDto).ToList());
    }
    private void Log(Guid id, string action, Guid? revisionId, object detail) =>
        uow.Repository.Add(new SheetActivity { WorkbookId = id, UserId = current.Id, Action = action,
            RevisionId = revisionId, OccurredAtUtc = DateTime.UtcNow, DetailJson = Serialize(detail) });
    private static void Page(QueryParams query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 100);
        if (query.Page > int.MaxValue / query.PageSize) throw new SheetRuleException("Sayfa numarası geçersiz.");
    }
    public async Task<PagedResult<SheetListDto>> ListAsync(SheetQuery q, CancellationToken ct)
    {
        var p = await Permissions(ct);
        if (!p.View) throw new SheetRuleException("Görüntüleme yetkisi gerekli.", 403);
        Page(q);
        var all = p.Manage || await (from ur in Query<UserRole>() join mr in Query<MenuRole>() on ur.RoleId equals mr.RoleId
            join role in Query<Role>() on ur.RoleId equals role.Id
            join menu in Query<Menu>() on mr.MenuId equals menu.Id
            where ur.UserId == p.Id && !role.IsDeleted && menu.Name == "SheetsManage" && mr.HasView select mr.Id).AnyAsync(ct);
        var books = Visible(p.Id, all).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search)) books = books.Where(x => x.Name.Contains(q.Search.Trim()));
        if (q.OwnerId.HasValue) books = books.Where(x => x.OwnerId == q.OwnerId);
        if (!string.IsNullOrWhiteSpace(q.OwnerName)) books = books.Where(x => Query<User>().Any(u => u.Id == x.OwnerId && u.Name.Contains(q.OwnerName.Trim())));
        if (q.OwnedOnly) books = books.Where(x => x.OwnerId == p.Id);
        if (q.UpdatedFrom.HasValue) books = books.Where(x => x.UpdatedAtUtc >= q.UpdatedFrom);
        if (q.UpdatedTo.HasValue) books = books.Where(x => x.UpdatedAtUtc <= q.UpdatedTo);
        var total = await books.CountAsync(ct);
        books = q.Sort == "name" ? (q.Desc ? books.OrderByDescending(x => x.Name).ThenBy(x => x.Id) : books.OrderBy(x => x.Name).ThenBy(x => x.Id))
            : (q.Desc ? books.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id) : books.OrderBy(x => x.UpdatedAtUtc).ThenBy(x => x.Id));
        var items = await (from b in books.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            join user in Query<User>() on b.OwnerId equals user.Id
            select new SheetListDto(b.Id, b.Name, b.OwnerId, user.Name, b.UpdatedAtUtc, p.Edit, p.Manage || b.OwnerId == p.Id)).ToListAsync(ct);
        return new(items, total, q.Page, q.PageSize);
    }
    public async Task<SheetWorkbookDto> OpenAsync(Guid id, Guid? revisionId, CancellationToken ct)
    {
        var book = await Require(id, "view", ct);
        return await Dto(book, await Revision(id, revisionId ?? book.ActiveRevisionId, ct), ct);
    }
    public async Task<SheetWorkbookDto> CreateAsync(SheetCreateDto dto, CancellationToken ct)
    {
        var p = await Permissions(ct);
        if (!p.Create) throw new SheetRuleException("Tablo oluşturma yetkisi gerekli.", 403);
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 200) throw new SheetRuleException("Tablo adı 1–200 karakter olmalıdır.");
        var snapshot = new WorkbookSnapshot { Worksheets = [NewWorksheet(Guid.NewGuid(), "Sayfa 1", dto.Rows, dto.Columns)] };
        Validate(snapshot);
        var now = DateTime.UtcNow;
        var book = new SheetWorkbook { Id = Guid.NewGuid(), Name = dto.Name.Trim(), OwnerId = p.Id,
            CreatedAtUtc = now, UpdatedAtUtc = now, ActiveRevisionId = Guid.NewGuid() };
        var revision = new SheetRevision { Id = book.ActiveRevisionId, WorkbookId = book.Id, CreatedBy = p.Id,
            CreatedAtUtc = now, SnapshotJson = Serialize(snapshot) };
        uow.Repository.Add(book);
        uow.Repository.Add(revision);
        Log(book.Id, "create", revision.Id, new { book.Name, dto.Rows, dto.Columns });
        await uow.Repository.CompleteAsync(ct);
        return await Dto(book, revision, ct);
    }
    public async Task<SheetWindowDto> WindowAsync(Guid id, SheetWindowQuery query, CancellationToken ct)
    {
        await Require(id, "view", ct);
        if (query.StartRow < 0 || query.Count is < 1 or > SheetLimits.WindowRows || query.Structure.Count > 2000)
            throw new SheetRuleException("Satır penceresi geçersiz.");
        var snapshot = Deserialize<WorkbookSnapshot>((await Revision(id, query.RevisionId, ct)).SnapshotJson);
        ApplyStructure(snapshot, query.Structure);
        var sheet = snapshot.Worksheets.SingleOrDefault(x => x.Id == query.WorksheetId) ?? throw new SheetRuleException("Çalışma sayfası bulunamadı.");
        var total = Count(sheet.Rows);
        if (query.StartRow >= total) return new(query.StartRow, total, []);
        var physicalRows = Enumerable.Range(query.StartRow, Math.Min(query.Count, total - query.StartRow))
            .Select(x => Physical(sheet.Rows, x)).ToHashSet();
        var ids = physicalRows.Select(x => x / SheetLimits.BlockRows).Distinct().Where(sheet.Blocks.ContainsKey).SelectMany(x => sheet.Blocks[x]).ToArray();
        var cells = new List<SheetCellDto>();
        var occupied = new HashSet<(int, int)>();
        var responseSize = 0;
        await foreach (var block in Query<SheetDataBlock>().AsNoTracking().Where(x => x.WorkbookId == id && ids.Contains(x.Id)).AsAsyncEnumerable().WithCancellation(ct))
            foreach (var cell in Deserialize<List<StoredSheetCell>>(block.CellsJson))
            {
                if (!physicalRows.Contains(cell.RowId)) continue;
                var row = Logical(sheet.Rows, cell.RowId);
                var col = Logical(sheet.Columns, cell.ColumnId);
                if (row.HasValue && col.HasValue)
                {
                    var dto = new SheetCellDto { Row = row.Value, Column = col.Value, Value = cell.Value, Kind = cell.Kind,
                        Style = cell.Style ?? sheet.RowStyles.GetValueOrDefault(cell.RowId) ?? sheet.ColumnStyles.GetValueOrDefault(cell.ColumnId) };
                    responseSize += Serialize(dto).Length;
                    if (responseSize > 32 * 1024 * 1024)
                        throw new SheetRuleException("Satır aralığı çok büyük; aralık boyutunu azaltın.", 413);
                    cells.Add(dto);
                    occupied.Add((row.Value, col.Value));
                }
            }
        foreach (var rowId in physicalRows)
        {
            var row = Logical(sheet.Rows, rowId)!.Value;
            for (var col = 0; col < Count(sheet.Columns); col++)
            {
                if (occupied.Contains((row, col))) continue;
                var inherited = sheet.RowStyles.GetValueOrDefault(rowId) ?? sheet.ColumnStyles.GetValueOrDefault(Physical(sheet.Columns, col));
                if (inherited != null)
                {
                    var dto = new SheetCellDto { Row = row, Column = col, Style = inherited };
                    responseSize += Serialize(dto).Length;
                    if (responseSize > 32 * 1024 * 1024)
                        throw new SheetRuleException("Satır aralığı çok büyük; aralık boyutunu azaltın.", 413);
                    cells.Add(dto);
                }
            }
        }
        return new(query.StartRow, total, cells) {
            RowHeights = sheet.RowHeights.Where(x => physicalRows.Contains(x.Key)).ToDictionary(x => Logical(sheet.Rows, x.Key)!.Value, x => x.Value)
        };
    }
    public async Task<SheetWorkbookDto> SaveAsync(Guid id, SheetSaveDto dto, CancellationToken ct)
        => await SaveCoreAsync(id, dto, true, ct);

    private async Task<SheetWorkbookDto> SaveCoreAsync(Guid id, SheetSaveDto dto, bool publish, CancellationToken ct)
    {
        if (dto.Cells == null || dto.Structure == null)
            throw new SheetRuleException("Hücre ve yapı değişiklikleri liste olmalıdır.");
        await Require(id, "edit", ct);
        if (dto.Cells.Count > SheetLimits.ChangesPerSave || dto.Structure.Count > 2000)
            throw new SheetRuleException("Bir kayıtta en fazla 50.000 hücre ve 2.000 yapı işlemi gönderilebilir.");
        // Serialize publication with deletion and other saves; stale base revisions remain valid.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var book = await db.Set<SheetWorkbook>().FromSqlInterpolated(
            $"SELECT * FROM [sheets].[Workbooks] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct)
            ?? throw new SheetRuleException("Tablo silinmiş.", 404);
        await Require(id, "edit", ct);
        var basis = await Revision(id, dto.BaseRevisionId, ct);
        var snapshot = Deserialize<WorkbookSnapshot>(basis.SnapshotJson);
        ApplyStructure(snapshot, dto.Structure);
        Validate(snapshot);
        var pending = dto.Cells.GroupBy(cell =>
        {
            ValidateCell(cell);
            var sheet = snapshot.Worksheets.SingleOrDefault(x => x.Id == cell.WorksheetId) ?? throw new SheetRuleException("Çalışma sayfası bulunamadı.");
            return (sheet.Id, Block: Physical(sheet.Rows, cell.Row) / SheetLimits.BlockRows);
        });
        foreach (var group in pending)
        {
            var sheet = snapshot.Worksheets.Single(x => x.Id == group.Key.Id);
            var changes = group.GroupBy(x => (Physical(sheet.Rows, x.Row), Physical(sheet.Columns, x.Column)))
                .ToDictionary(x => x.Key, x => x.Last());
            var references = new List<Guid>();
            if (sheet.Blocks.TryGetValue(group.Key.Block, out var oldIds))
            {
                foreach (var blockId in oldIds)
                {
                    var source = await Query<SheetDataBlock>().AsNoTracking().SingleAsync(x => x.WorkbookId == id && x.Id == blockId, ct);
                    var oldCells = Deserialize<List<StoredSheetCell>>(source.CellsJson);
                    var changed = false;
                    var next = new List<StoredSheetCell>();
                    foreach (var cell in oldCells)
                    {
                        if (!changes.Remove((cell.RowId, cell.ColumnId), out var change)) { next.Add(cell); continue; }
                        changed = true;
                        if (change.Value != null || change.Style != null)
                            next.Add(new() { RowId = cell.RowId, ColumnId = cell.ColumnId, Value = change.Value, Kind = change.Kind, Style = change.Style });
                    }
                    if (!changed) references.Add(blockId);
                    else references.AddRange(await StoreCellsAsync(id, next, ct));
                }
            }
            references.AddRange(await StoreCellsAsync(id, changes.Where(x => x.Value.Value != null || x.Value.Style != null)
                .Select(x => new StoredSheetCell { RowId = x.Key.Item1, ColumnId = x.Key.Item2, Value = x.Value.Value, Kind = x.Value.Kind, Style = x.Value.Style }), ct));
            if (references.Count == 0) sheet.Blocks.Remove(group.Key.Block);
            else sheet.Blocks[group.Key.Block] = references;
        }
        var revision = new SheetRevision { Id = Guid.NewGuid(), WorkbookId = id, BaseRevisionId = basis.Id,
            CreatedBy = current.Id, CreatedAtUtc = DateTime.UtcNow, SnapshotJson = Serialize(snapshot), IsDraft = !publish };
        uow.Repository.Add(revision);
        var previous = book.ActiveRevisionId;
        if (publish)
        {
            book.ActiveRevisionId = revision.Id;
            book.UpdatedAtUtc = revision.CreatedAtUtc;
        }
        Log(id, publish ? "save" : "draft", revision.Id, new { baseRevisionId = basis.Id, replacedRevisionId = previous,
            structure = dto.Structure, cells = dto.Cells });
        await uow.Repository.CompleteAsync(ct);
        await transaction.CommitAsync(ct);
        return await Dto(book, revision, ct);
    }
    private async Task<List<Guid>> StoreCellsAsync(Guid workbookId, IEnumerable<StoredSheetCell> cells, CancellationToken ct)
    {
        var ids = new List<Guid>();
        var pending = new List<StoredSheetCell>();
        var size = 2;
        async Task Flush()
        {
            if (pending.Count == 0) return;
            var block = new SheetDataBlock { Id = Guid.NewGuid(), WorkbookId = workbookId, CellsJson = Serialize(pending) };
            uow.Repository.Add(block);
            await uow.Repository.CompleteAsync(ct);
            db.Entry(block).State = EntityState.Detached;
            ids.Add(block.Id);
            pending.Clear();
            size = 2;
        }
        foreach (var cell in cells)
        {
            var cellSize = Serialize(cell).Length + 1;
            if (size + cellSize > 512 * 1024) await Flush();
            pending.Add(cell);
            size += cellSize;
        }
        await Flush();
        return ids;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await Require(id, "manage", ct);
        // The parent lock matches Save/Import, preventing a late save from recreating deleted data.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var book = await db.Set<SheetWorkbook>().FromSqlInterpolated(
            $"SELECT * FROM [sheets].[Workbooks] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct);
        if (book == null) throw new SheetRuleException("Tablo bulunamadı.", 404);
        await Require(id, "manage", ct);
        uow.Repository.HardDelete(book);
        await uow.Repository.CompleteAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private IQueryable<long> EligibleIds() =>
        (from user in Query<User>() join ur in Query<UserRole>() on user.Id equals ur.UserId
         join role in Query<Role>() on ur.RoleId equals role.Id
         join mr in Query<MenuRole>() on ur.RoleId equals mr.RoleId
         join menu in Query<Menu>() on mr.MenuId equals menu.Id
         where user.IsActive && !user.IsDeleted && !role.IsDeleted &&
             new[] { "SheetsList", "SheetsCreate", "SheetsManage" }.Contains(menu.Name) && (mr.HasView || mr.HasEdit)
         select user.Id).Distinct();
    public async Task<PagedResult<SheetUserDto>> EligibleUsersAsync(QueryParams q, CancellationToken ct)
    {
        var p = await Permissions(ct);
        if (!p.View && !p.Create) throw new SheetRuleException("MGS Tablolar yetkisi gerekli.", 403);
        Page(q);
        var users = Query<User>().AsNoTracking().Where(x => EligibleIds().Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(q.Search)) users = users.Where(x => x.Name.Contains(q.Search.Trim()));
        return new(await users.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new SheetUserDto(x.Id, x.Name)).ToListAsync(ct), await users.CountAsync(ct), q.Page, q.PageSize);
    }
    public async Task<List<SheetUserDto>> AccessAsync(Guid id, CancellationToken ct)
    {
        await Require(id, "manage", ct);
        return await (from a in Query<SheetAccess>() join user in Query<User>() on a.UserId equals user.Id
            where a.WorkbookId == id select new SheetUserDto(user.Id, user.Name)).ToListAsync(ct);
    }
    public async Task GrantAsync(Guid id, long userId, bool grant, CancellationToken ct)
    {
        await Require(id, "manage", ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var book = await db.Set<SheetWorkbook>().FromSqlInterpolated(
            $"SELECT * FROM [sheets].[Workbooks] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct)
            ?? throw new SheetRuleException("Tablo bulunamadı.", 404);
        await Require(id, "manage", ct);
        if (book.OwnerId == userId) throw new SheetRuleException("Sahibin erişimi değiştirilemez.");
        if (grant && !await EligibleIds().AnyAsync(x => x == userId, ct)) throw new SheetRuleException("Kullanıcı MGS Tablolar yetkisine sahip değil.");
        var access = await Query<SheetAccess>().SingleOrDefaultAsync(x => x.WorkbookId == id && x.UserId == userId, ct);
        if (grant && access == null) uow.Repository.Add(new SheetAccess { WorkbookId = id, UserId = userId,
            GrantedBy = current.Id, GrantedAtUtc = DateTime.UtcNow });
        if (!grant && access != null) uow.Repository.HardDelete(access);
        Log(id, grant ? "share" : "unshare", null, new { userId });
        await uow.Repository.CompleteAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task<PagedResult<SheetActivityDto>> ActivitiesAsync(Guid id, QueryParams q, CancellationToken ct)
    {
        await Require(id, "view", ct);
        Page(q);
        var logs = Query<SheetActivity>().AsNoTracking().Where(x => x.WorkbookId == id);
        var items = await (from log in logs.OrderByDescending(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            join user in Query<User>() on log.UserId equals user.Id
            select new SheetActivityDto(log.Id, log.UserId, user.Name, log.Action, log.RevisionId, log.OccurredAtUtc, log.DetailJson)).ToListAsync(ct);
        return new(items, await logs.CountAsync(ct), q.Page, q.PageSize);
    }
}
