using Business.Interfaces.PeriodicReports;
using Business.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Business.Services.PeriodicReports
{
    public sealed class ReportSqlValidator : IReportSqlValidator
    {
        public SqlValidationResult Validate(string sqlQuery)
        {
            if (string.IsNullOrWhiteSpace(sqlQuery))
                return Invalid("SQL sorgusu zorunludur.");

            var parser = new TSql170Parser(initialQuotedIdentifiers: true);
            using var reader = new StringReader(sqlQuery);
            var fragment = parser.Parse(reader, out var parseErrors);

            if (parseErrors.Count > 0)
                return Invalid("SQL söz dizimi geçersiz.");

            if (fragment is not TSqlScript script ||
                script.Batches.Count != 1 ||
                script.Batches[0].Statements.Count != 1)
            {
                return Invalid("Yalnızca tek bir SQL statement çalıştırılabilir.");
            }

            if (script.Batches[0].Statements[0] is not SelectStatement selectStatement)
                return Invalid("Yalnızca SELECT sorgularına izin verilir.");

            if (selectStatement.Into != null)
                return Invalid("SELECT INTO ile tablo oluşturulamaz.");

            var visitor = new ReadOnlySqlVisitor();
            selectStatement.Accept(visitor);

            return visitor.Errors.Count == 0
                ? SqlValidationResult.Success
                : new SqlValidationResult(false, visitor.Errors.Distinct().ToArray());
        }

        private static SqlValidationResult Invalid(string error) =>
            new(false, new[] { error });

        private sealed class ReadOnlySqlVisitor : TSqlFragmentVisitor
        {
            public List<string> Errors { get; } = new();

            public override void ExplicitVisit(OpenRowsetTableReference node) =>
                Errors.Add("OPENROWSET kullanımına izin verilmez.");

            public override void ExplicitVisit(OpenQueryTableReference node) =>
                Errors.Add("OPENQUERY kullanımına izin verilmez.");

            public override void ExplicitVisit(BulkOpenRowset node) =>
                Errors.Add("BULK erişimine izin verilmez.");

            public override void ExplicitVisit(AdHocDataSource node) =>
                Errors.Add("Ad-hoc veri kaynağı kullanımına izin verilmez.");
        }
    }
}
