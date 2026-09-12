using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Ekb;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;

namespace Data.Concrete.EfCore.Queries
{
    public static class ArchiveProductSearch
    {
        /// <summary>
        /// Returns a composable subquery; product snapshots contain IDs, so names/codes
        /// are resolved from the current product catalogue without loading archive JSON.
        /// Keep the search text's casing: the database's CI collation handles comparisons;
        /// invariant lowercasing conflicts with Turkish I/ı and İ/i mappings.
        /// </summary>
        public static IQueryable<long> MatchingArchiveIds<TArchive>(AppDataContext context, string pattern)
        {
            // SQL identifiers come only from this fixed tenant allowlist, never request data.
            var (table, productsColumn) = typeof(TArchive) switch
            {
                var type when type == typeof(YkbWorkFlowArchive) =>
                    ("[ykb].[YkbWorkFlowArchive]", "[YkbServicesRequestProductsJson]"),
                var type when type == typeof(QnbWorkFlowArchive) =>
                    ("[qnb].[QnbWorkFlowArchive]", "[QnbServicesRequestProductsJson]"),
                var type when type == typeof(EkbWorkFlowArchive) =>
                    ("[ekb].[EkbWorkFlowArchive]", "[EkbServicesRequestProductsJson]"),
                var type when type == typeof(WorkFlowArchive) =>
                    ("[dbo].[WorkFlowArchives]", "[ServicesRequestProductsJson]"),
                _ => throw new ArgumentException("Unsupported archive type.", nameof(TArchive))
            };

            var sql = $"""
                SELECT a.[Id] AS [Value]
                FROM {table} AS a
                WHERE EXISTS (
                    SELECT 1
                    FROM OPENJSON(
                        CASE WHEN ISJSON(a.{productsColumn}) = 1
                            THEN a.{productsColumn} ELSE N'[]' END
                    ) WITH ([ProductId] nvarchar(64) '$.ProductId') AS item
                    INNER JOIN [dbo].[Product] AS p
                        ON p.[Id] = TRY_CONVERT(bigint, item.[ProductId])
                    WHERE p.[ProductCode] LIKE @archiveProductPattern
                        OR p.[Description] LIKE @archiveProductPattern
                )
                """;

            return context.Database.SqlQueryRaw<long>(sql,
                new SqlParameter("@archiveProductPattern", pattern));
        }
    }
}
