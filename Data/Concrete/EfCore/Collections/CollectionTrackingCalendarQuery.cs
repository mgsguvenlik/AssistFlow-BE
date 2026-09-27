using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;

namespace Data.Concrete.EfCore.Collections;

/// <summary>Parameterized, composable month rows; no calendar table or database mutation.</summary>
public static class CollectionTrackingCalendarQuery
{
    public static IQueryable<DateOnly> Months(AppDataContext db, DateOnly first, int count)
    {
        if (count is < 1 or > 600 || first.Day != 1 || first.Year >= 9999)
            throw new ArgumentOutOfRangeException(nameof(count), "Dönem aralığı geçersiz.");
        var offsets = JsonSerializer.Serialize(Enumerable.Range(0, count));
        return db.Database.SqlQuery<DateOnly>($"""
            SELECT DATEADD(month, CONVERT(int, j.[value]), CAST({first} AS date)) AS [Value]
            FROM OPENJSON({offsets}) AS j
            """);
    }
}
