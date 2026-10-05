using Business.Interfaces;
using Business.Services.Sheets;
using Business.UnitOfWork;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Data.Concrete.EfCore.Configurations;
using Data.Migrations;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Model.Concrete;
using Model.Concrete.Sheets;
using Model.Dtos.Auth;
using Model.Dtos.Sheets;

internal sealed class TestUser(long id) : ICurrentUser
{
    public long Id => id;
    public string? Email => null;
    public string? Name => "Test " + id;
    public ValueTask<CurrentUserDto?> GetAsync(CancellationToken ct = default) => ValueTask.FromResult<CurrentUserDto?>(new() { Id = id, IsAuthenticated = true });
}

// Only the shared tables relevant to Sheets are needed in the disposable database.
internal class SharedTestContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        var keep = new[] { typeof(User), typeof(Role), typeof(Menu), typeof(UserRole), typeof(MenuRole) };
        foreach (var property in typeof(AppDataContext).GetProperties())
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var type = property.PropertyType.GetGenericArguments()[0];
                if (!keep.Contains(type)) model.Ignore(type);
            }
        model.Entity<User>().Ignore(x => x.Tenant).Ignore(x => x.UserRoles);
        model.Entity<Role>().Ignore(x => x.UserRoles).Ignore(x => x.MenuRoles);
        model.Entity<Menu>().Ignore(x => x.MenuRoles);
        model.Entity<UserRole>().Ignore(x => x.User).Ignore(x => x.Role);
        model.Entity<MenuRole>().Ignore(x => x.Menu).Ignore(x => x.Role);
    }
}
internal sealed class SheetsTestContext(DbContextOptions<AppDataContext> options) : SharedTestContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        SheetsConfiguration.Configure(model);
    }
}
internal sealed class SheetsServiceTests(string connection)
{
    internal DbContextOptions<AppDataContext> Options => new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection).Options;
    private SheetsTestContext Db() => new(Options);
    private static SheetsService Service(SheetsTestContext db, long user) => new(new UnitOfWork(new Repository(db)), db, new TestUser(user));
    private long owner, editor, viewer, outsider, manager, inactive, noMenu;

    public async Task<int> Run()
    {
        await Setup();
        Guid bookId;
        Guid original;
        Guid savedA;
        await using (var db = Db())
        {
            var service = Service(db, owner);
            var book = await service.CreateAsync(new() { Name = "Large sparse", Rows = 1_000_000, Columns = 100 }, default);
            bookId = book.Id; original = book.RevisionId;
            Check.That(await db.Set<SheetDataBlock>().CountAsync() == 0, "No blank cell materialization");
            await service.GrantAsync(bookId, editor, true, default);
            await service.GrantAsync(bookId, viewer, true, default);
            Check.That(!(await service.EligibleUsersAsync(new(), default)).Items.Any(x => x.Id == inactive || x.Id == noMenu), "Share users are eligible and active");
            Check.That((await service.AccessAsync(bookId, default)).Count == 2, "Visibility grants");
            var listed = await service.ListAsync(new() { OwnerName = "User 0", Search = "Large", OwnedOnly = true, PageSize = 1 }, default);
            Check.That(listed.Items.Single().Id == bookId && listed.TotalCount == 1, "Server-side owner, name, ownership and paging filters");
        }
        await using (var db = Db())
        {
            var service = Service(db, outsider);
            await Check.Reject(() => service.OpenAsync(bookId, null, default), 404, "Uninvited viewer cannot open");
            await Check.Reject(() => service.WindowAsync(bookId, new() { RevisionId = original }, default), 404, "Window authorization");
        }
        await using (var db = Db())
        {
            var service = Service(db, viewer);
            var book = await service.OpenAsync(bookId, null, default);
            Check.That(!book.CanEdit, "View-only user");
            await Check.Reject(() => service.SaveAsync(bookId, new() { BaseRevisionId = original }, default), 403, "Viewer cannot save");
            await Check.Reject(() => service.ExportAsync(bookId, null, default), 403, "Viewer cannot download");
            await Check.Reject(() => service.GrantAsync(bookId, outsider, true, default), 403, "Viewer cannot share");
        }
        await using (var db = Db())
        {
            var service = Service(db, manager);
            var book = await service.OpenAsync(bookId, null, default);
            Check.That(book.CanEdit && book.CanManage, "Manager sees and edits without an invitation");
            await service.GrantAsync(bookId, outsider, true, default);
            await service.GrantAsync(bookId, outsider, false, default);
        }
        await using (var db = Db())
        {
            var service = Service(db, owner);
            var sheet = (await service.OpenAsync(bookId, original, default)).Worksheets[0];
            var result = await service.SaveAsync(bookId, new() { BaseRevisionId = original, Cells = [
                new() { WorksheetId = sheet.Id, Row = 999999, Column = 99, Value = "A last row" }] }, default);
            savedA = result.RevisionId;
            var read = await service.WindowAsync(bookId, new() { RevisionId = savedA, WorksheetId = sheet.Id, StartRow = 999999, Count = 1 }, default);
            Check.That(read.Cells.Single().Value == "A last row", "Last row and column window");
        }
        await using (var db = Db())
        {
            var service = Service(db, editor);
            var sheet = (await service.OpenAsync(bookId, original, default)).Worksheets[0];
            var savedB = await service.SaveAsync(bookId, new() { BaseRevisionId = original, Cells = [
                new() { WorksheetId = sheet.Id, Row = 0, Column = 0, Value = "B wins" }] }, default);
            Check.That((await service.OpenAsync(bookId, null, default)).RevisionId == savedB.RevisionId, "Last successful workbook save wins");
            Check.That((await service.WindowAsync(bookId, new() { RevisionId = savedB.RevisionId, WorksheetId = sheet.Id, StartRow = 999999, Count = 1 }, default)).Cells.Count == 0, "Unloaded A data is replaced by B opening snapshot");
            Check.That((await service.WindowAsync(bookId, new() { RevisionId = savedA, WorksheetId = sheet.Id, StartRow = 999999, Count = 1 }, default)).Cells.Single().Value == "A last row", "Opening versions remain immutable");
            Check.That((await service.ActivitiesAsync(bookId, new(), default)).Items.Count >= 4, "Action log");
        }

        // Both editors submit the same opening version concurrently. Exactly one complete snapshot wins.
        Guid concurrentBase;
        Guid concurrentSheet;
        await using (var db = Db())
        {
            var opened = await Service(db, owner).OpenAsync(bookId, null, default);
            concurrentBase = opened.RevisionId; concurrentSheet = opened.Worksheets[0].Id;
        }
        async Task<SheetWorkbookDto> ConcurrentSave(long user, int row)
        {
            await using var db = Db();
            return await Service(db, user).SaveAsync(bookId, new() { BaseRevisionId = concurrentBase, Cells = [
                new() { WorksheetId = concurrentSheet, Row = row, Column = 0, Value = "concurrent " + user }] }, default);
        }
        var concurrent = await Task.WhenAll(ConcurrentSave(owner, 5), ConcurrentSave(editor, 6));
        await using (var db = Db())
        {
            var service = Service(db, owner);
            var active = await service.OpenAsync(bookId, null, default);
            Check.That(concurrent.Any(x => x.RevisionId == active.RevisionId), "One concurrent save is active");
            var cells = await service.WindowAsync(bookId, new() { RevisionId = active.RevisionId, WorksheetId = concurrentSheet, StartRow = 5, Count = 2 }, default);
            Check.That(cells.Cells.Count == 1, "Concurrent saves never merge opening snapshots");
            var big = new string('z', 32700);
            var chunks = await service.SaveAsync(bookId, new() { BaseRevisionId = active.RevisionId, Cells = Enumerable.Range(0, 30).Select(c =>
                new SheetCellChange { WorksheetId = concurrentSheet, Row = 8, Column = c, Value = big }).ToList() }, default);
            Check.That(await db.Set<SheetDataBlock>().AllAsync(x => x.CellsJson.Length <= 512 * 1024), "Immutable chunks have bounded byte size");
            var manifest = SheetSnapshotEngine.Deserialize<WorkbookSnapshot>((await db.Set<SheetRevision>().SingleAsync(x => x.Id == chunks.RevisionId)).SnapshotJson);
            Check.That(manifest.Worksheets[0].Blocks[0].Count > 1, "Large row blocks split into bounded chunks");
        }
        await SheetHttpTests.Run(Options, bookId, owner, viewer, outsider);

        Guid formatBook;
        Guid formatRevision;
        Guid formatSheet;
        await using (var db = Db())
        {
            var service = Service(db, owner);
            var book = await service.CreateAsync(new() { Name = "Excel fidelity", Rows = 300, Columns = 4 }, default);
            formatBook = book.Id; formatSheet = book.Worksheets[0].Id;
            var sheet2 = Guid.NewGuid();
            var style = new SheetCellStyle { Bold = true, Italic = true, Color = "#AABBCC", Background = "#112233", FontName = "Arial", FontSize = 14, Alignment = "center", NumberFormat = "0.00", Wrap = true };
            var first = await service.SaveAsync(book.Id, new() { BaseRevisionId = book.RevisionId, Structure = [
                new() { Type = "renameSheet", WorksheetId = formatSheet, Name = "Veriler" },
                new() { Type = "addSheet", WorksheetId = sheet2, Name = "İkinci", Rows = 2, Columns = 2 },
                new() { Type = "columnWidth", WorksheetId = formatSheet, Index = 1, Size = 155 },
                new() { Type = "rowHeight", WorksheetId = formatSheet, Index = 0, Size = 45 }], Cells = [
                new() { WorksheetId = formatSheet, Row = 0, Column = 0, Value = "=literal", Kind = "text" },
                new() { WorksheetId = formatSheet, Row = 0, Column = 1, Value = "123.45", Kind = "number", Style = style },
                new() { WorksheetId = formatSheet, Row = 0, Column = 2, Value = "true", Kind = "boolean" },
                new() { WorksheetId = formatSheet, Row = 0, Column = 3, Value = "2026-10-05T12:00:00", Kind = "date" },
                new() { WorksheetId = formatSheet, Row = 200, Column = 0, Value = "outside window" },
                new() { WorksheetId = sheet2, Row = 0, Column = 0, Value = "İstanbul / Türkçe" }
            ] }, default);
            var before = SheetSnapshotEngine.Deserialize<WorkbookSnapshot>((await db.Set<SheetRevision>().SingleAsync(x => x.Id == first.RevisionId)).SnapshotJson);
            var modified = await service.SaveAsync(book.Id, new() { BaseRevisionId = first.RevisionId, Cells = [
                new() { WorksheetId = formatSheet, Row = 1, Column = 0, Value = "only changed block" }] }, default);
            var after = SheetSnapshotEngine.Deserialize<WorkbookSnapshot>((await db.Set<SheetRevision>().SingleAsync(x => x.Id == modified.RevisionId)).SnapshotJson);
            Check.That(before.Worksheets[0].Blocks[200 / SheetLimits.BlockRows].SequenceEqual(after.Worksheets[0].Blocks[200 / SheetLimits.BlockRows]), "Unchanged data chunks are reused");
            var moved = await service.SaveAsync(book.Id, new() { BaseRevisionId = modified.RevisionId, Structure = [
                new() { Type = "insertRows", WorksheetId = formatSheet, Index = 0, Count = 2 },
                new() { Type = "insertColumns", WorksheetId = formatSheet, Index = 0 },
                new() { Type = "moveSheet", WorksheetId = sheet2, Index = 0 }] }, default);
            formatRevision = moved.RevisionId;
            var window = await service.WindowAsync(book.Id, new() { RevisionId = moved.RevisionId, WorksheetId = formatSheet, StartRow = 2, Count = 1 }, default);
            Check.That(window.Cells.Single(x => x.Column == 2).Style!.Bold, "Style follows stable cell after insert");
            Check.That(moved.Worksheets[1].ColumnWidths[2] == 155 && moved.Worksheets[1].RowHeights[2] == 45, "Measurements follow insert");
            var path = await service.ExportAsync(book.Id, null, default);
            try
            {
                using (var doc = SpreadsheetDocument.Open(path, false))
                {
                    var errors = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2013).Validate(doc).Take(10).ToArray();
                    Check.That(errors.Length == 0, "Valid XLSX: " + string.Join("; ", errors.Select(x => x.Description)));
                }
                await using var stream = File.OpenRead(path);
                var imported = await service.CreateFromExcelAsync("Roundtrip", stream, default);
                Check.That(imported.Worksheets.Select(x => x.Name).SequenceEqual(new[] { "İkinci", "Veriler" }), "Excel worksheet order and names");
                var importedSheet = imported.Worksheets[1];
                Check.That(importedSheet.Rows == 302 && importedSheet.Columns == 5, "Excel dimensions preserved");
                var cells = (await service.WindowAsync(imported.Id, new() { RevisionId = imported.RevisionId, WorksheetId = importedSheet.Id, StartRow = 2, Count = 1 }, default)).Cells;
                var numeric = cells.Single(x => x.Column == 2);
                Check.That(numeric.Value == "123.45" && numeric.Kind == "number" && numeric.Style!.Background == "#112233" && numeric.Style.Color == "#AABBCC" && numeric.Style.Bold && numeric.Style.FontName == "Arial", "Excel values and formats roundtrip");
                Check.That(cells.Single(x => x.Column == 1).Value == "=literal", "Literal text is not a formula");
            }
            finally { File.Delete(path); }
        }

        await using (var db = Db())
        {
            var service = Service(db, owner);
            var path = await service.ExportAsync(formatBook, formatRevision, default);
            try
            {
                await using var stream = File.OpenRead(path);
                var staged = await service.ImportAsync(formatBook, new() { BaseRevisionId = formatRevision }, stream, default);
                Check.That(staged.Worksheets.Count == 4, "Import appends worksheets");
                Check.That((await service.OpenAsync(formatBook, null, default)).RevisionId == formatRevision, "Import waits for explicit Save");
                await Check.Reject(() => Service(db, manager).OpenAsync(formatBook, staged.RevisionId, default), 404, "Private imported draft");
                var published = await service.SaveAsync(formatBook, new() { BaseRevisionId = staged.RevisionId }, default);
                Check.That((await service.OpenAsync(formatBook, null, default)).Worksheets.Count == published.Worksheets.Count, "Save publishes import");
                await service.DeleteAsync(formatBook, default);
                Check.That(!await db.Set<SheetDataBlock>().AnyAsync(x => x.WorkbookId == formatBook) &&
                    !await db.Set<SheetRevision>().AnyAsync(x => x.WorkbookId == formatBook) &&
                    !await db.Set<SheetActivity>().AnyAsync(x => x.WorkbookId == formatBook), "Physical cascading deletion");
                await Check.Reject(() => service.SaveAsync(formatBook, new() { BaseRevisionId = formatRevision }, default), 404, "Deleted workbook cannot be resurrected");
            }
            finally { File.Delete(path); }
        }
        await using (var db = Db())
        {
            var service = Service(db, owner);
            using var fixture = SheetExcelFixture.Create();
            var book = await service.CreateFromExcelAsync("Inherited formatting", fixture, default);
            var sheet = book.Worksheets.Single();
            var window = await service.WindowAsync(book.Id, new() { RevisionId = book.RevisionId, WorksheetId = sheet.Id, Count = 4 }, default);
            Check.That(window.Cells.Single(x => x.Row == 0 && x.Column == 1).Value == "42" && window.Cells.Single(x => x.Row == 0 && x.Column == 1).Style!.Bold, "Column style applies to populated cell");
            Check.That(window.Cells.Single(x => x.Row == 0 && x.Column == 1).Style!.Color == "#800000", "Standard indexed Excel color palette");
            Check.That(window.Cells.Single(x => x.Row == 3 && x.Column == 1).Style!.Bold, "Column style applies to blank cells without materializing storage");
            Check.That(window.Cells.Single(x => x.Row == 1 && x.Column == 1).Style!.Italic, "Row style takes precedence over column style");
            Check.That(window.RowHeights[1] == 40, "Window row height preserves point conversion");
            var path = await service.ExportAsync(book.Id, null, default);
            try
            {
                using (var document = SpreadsheetDocument.Open(path, false))
                    Check.That(!new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2013).Validate(document).Any(), "Inherited style XLSX validity");
                await using var stream = File.OpenRead(path);
                var imported = await service.CreateFromExcelAsync("Inherited roundtrip", stream, default);
                var read = await service.WindowAsync(imported.Id, new() { RevisionId = imported.RevisionId, WorksheetId = imported.Worksheets.Single().Id, Count = 4 }, default);
                Check.That(read.Cells.Single(x => x.Row == 3 && x.Column == 1).Style!.Bold && read.Cells.Single(x => x.Row == 1 && x.Column == 0).Style!.Italic, "Inherited row and column styles roundtrip");
            }
            finally { File.Delete(path); }
        }
        return 45;
    }

    private async Task Setup()
    {
        await using var shared = new SharedTestContext(Options);
        await shared.Database.EnsureCreatedAsync();
        await using var db = Db();
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddMgsSheets().UpOperations))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var menus = new[] { new Menu { Name = "SheetsList" }, new Menu { Name = "SheetsCreate" }, new Menu { Name = "SheetsManage" } };
        var rw = new Role { Name = "Arbitrary editor role", Code = "SOME_EDITOR" };
        var ro = new Role { Name = "Arbitrary viewer role", Code = "SOME_VIEWER" };
        var admin = new Role { Name = "Arbitrary management role", Code = "NOT_ADMIN" };
        db.AddRange(menus); db.AddRange(rw, ro, admin);
        var users = Enumerable.Range(0, 7).Select(i => new User { Name = "User " + i, Code = "SheetsTest" + i, PasswordHash = "test-fixture", IsActive = i != 5 }).ToArray();
        db.AddRange(users); await db.SaveChangesAsync();
        owner = users[0].Id; editor = users[1].Id; viewer = users[2].Id; outsider = users[3].Id; manager = users[4].Id; inactive = users[5].Id; noMenu = users[6].Id;
        foreach (var user in users.Take(6)) db.Add(new UserRole { UserId = user.Id, RoleId = user.Id == viewer ? ro.Id : user.Id == manager ? admin.Id : rw.Id });
        db.AddRange(new MenuRole { MenuId = menus[0].Id, RoleId = rw.Id, HasView = true, HasEdit = true },
            new MenuRole { MenuId = menus[1].Id, RoleId = rw.Id, HasView = true, HasEdit = true },
            new MenuRole { MenuId = menus[0].Id, RoleId = ro.Id, HasView = true },
            new MenuRole { MenuId = menus[2].Id, RoleId = admin.Id, HasView = true, HasEdit = true });
        await db.SaveChangesAsync();
    }
}
