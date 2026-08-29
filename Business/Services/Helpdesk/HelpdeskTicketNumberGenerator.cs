using Business.Interfaces.Helpdesk;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Business.Services.Helpdesk;

public sealed class HelpdeskTicketNumberGenerator(AppDataContext db) : IHelpdeskTicketNumberGenerator
{
    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTimeOffset.Now.Year;
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            MERGE helpdesk.TicketNumberSequence WITH (HOLDLOCK) AS target
            USING (SELECT @year AS [Year]) AS source
            ON target.[Year] = source.[Year]
            WHEN MATCHED THEN UPDATE SET LastNumber = target.LastNumber + 1
            WHEN NOT MATCHED THEN INSERT ([Year], LastNumber) VALUES (source.[Year], 1)
            OUTPUT inserted.LastNumber;
            """;
        var yearParameter = command.CreateParameter();
        yearParameter.ParameterName = "@year";
        yearParameter.DbType = DbType.Int32;
        yearParameter.Value = year;
        command.Parameters.Add(yearParameter);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var number = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        return $"HD-{year}-{number:000000}";
    }
}
