using Business.Services.Sheets;
using Model.Dtos.Sheets;
using static Business.Services.Sheets.SheetSnapshotEngine;

internal static class SheetSnapshotTests
{
    public static int Run()
    {
        var snapshot = new WorkbookSnapshot { Worksheets = [NewWorksheet(Guid.NewGuid(), "Sheet1", 1_000_000, 100)] };
        Validate(snapshot);
        Check.That(Serialize(snapshot).Length < 1000, "A million blank rows are one range");
        Check.Throws(() => NewWorksheet(Guid.NewGuid(), "bad", 1_000_001, 100), "Row limit");
        Check.Throws(() => NewWorksheet(Guid.NewGuid(), "bad", 1, 101), "Column limit");
        for (var i = 1; i < 10; i++) snapshot.Worksheets.Add(NewWorksheet(Guid.NewGuid(), "Sheet" + (i + 1), 1, 1));
        Validate(snapshot);
        Check.Throws(() => ApplyStructure(snapshot, [new() { Type = "addSheet", WorksheetId = Guid.NewGuid(), Name = "Eleven" }]), "Worksheet limit");

        var random = new Random(753);
        snapshot = new() { Worksheets = [NewWorksheet(Guid.NewGuid(), "Rows", 30, 10)] };
        var sheet = snapshot.Worksheets[0];
        var expected = Enumerable.Range(0, 30).Select(x => (long)x).ToList();
        for (var n = 0; n < 500; n++)
        {
            if (expected.Count < 3 || random.Next(2) == 0)
            {
                var index = random.Next(expected.Count + 1);
                var count = random.Next(1, 4);
                var next = sheet.NextRowId;
                ApplyStructure(snapshot, [new() { Type = "insertRows", WorksheetId = sheet.Id, Index = index, Count = count }]);
                expected.InsertRange(index, Enumerable.Range(0, count).Select(x => next + x));
            }
            else
            {
                var index = random.Next(expected.Count);
                ApplyStructure(snapshot, [new() { Type = "deleteRows", WorksheetId = sheet.Id, Index = index }]);
                expected.RemoveAt(index);
            }
            for (var i = 0; i < expected.Count; i++)
            {
                Check.That(Physical(sheet.Rows, i) == expected[i], "Stable row IDs");
                Check.That(Logical(sheet.Rows, expected[i]) == i, "Inverse row mapping");
            }
        }
        ApplyStructure(snapshot, [new() { Type = "insertColumns", WorksheetId = sheet.Id, Index = 3, Count = 2 }]);
        Check.That(Physical(sheet.Columns, 5) == 3, "Column insertion preserves IDs");
        ApplyStructure(snapshot, [new() { Type = "deleteColumns", WorksheetId = sheet.Id, Index = 1, Count = 4 }]);
        Check.That(Physical(sheet.Columns, 1) == 3, "Column deletion preserves IDs");
        Check.Throws(() => ValidateCell(new() { Kind = "number", Value = "NaN" }), "Non-finite numbers rejected");
        Check.Throws(() => ValidateCell(new() { Value = "v", Style = new() { Background = "url(javascript:bad)" } }), "Style validation");
        Check.Throws(() => ValidateCell(new() { Value = new string('x', 32768) }), "Excel text size");
        CheckDimensions();
        return 13;
    }

    private static void CheckDimensions()
    {
        var snapshot = new WorkbookSnapshot { Worksheets = [NewWorksheet(Guid.NewGuid(), "Boyutlar", 999_998, 99)] };
        var sheet = snapshot.Worksheets[0];
        ApplyStructure(snapshot, [
            new() { Type = "rowHeight", WorksheetId = sheet.Id, Index = 600, Size = 84 },
            new() { Type = "columnWidth", WorksheetId = sheet.Id, Index = 1, Size = 245 }]);
        var reopened = Deserialize<WorkbookSnapshot>(Serialize(snapshot));
        var saved = reopened.Worksheets[0];
        Check.That(saved.RowHeights[Physical(saved.Rows, 600)] == 84 && saved.ColumnWidths[Physical(saved.Columns, 1)] == 245,
            "Saved dimensions survive snapshot serialization beyond the initial row window");
        Check.That(saved.RowHeights.Count == 1 && !saved.RowHeights.ContainsKey(0), "Only explicit row size overrides are stored");

        ApplyStructure(reopened, [
            new() { Type = "insertRows", WorksheetId = saved.Id, Index = 0, Count = 2 },
            new() { Type = "insertColumns", WorksheetId = saved.Id, Index = 0 },
            new() { Type = "rowHeight", WorksheetId = saved.Id, Index = 602, Size = 112 }]);
        Check.That(saved.RowHeights[Physical(saved.Rows, 602)] == 112 && saved.RowHeights.Count == 1,
            "Later row sizing replaces the same stable row after insertion");
        Check.That(ToDto(saved).ColumnWidths[2] == 245, "Manual column width follows its column after insertion");

        foreach (var size in new[] { double.NaN, double.PositiveInfinity, 9d, 1001d })
            foreach (var type in new[] { "rowHeight", "columnWidth" })
                Check.Throws(() => ApplyStructure(reopened, [new() { Type = type, WorksheetId = saved.Id, Index = 0, Size = size }]),
                    "Dimensions reject non-finite and out-of-range sizes");
    }
}
