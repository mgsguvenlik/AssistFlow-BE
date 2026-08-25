using Business.Models;

namespace Business.Interfaces.PeriodicReports
{
    public interface IReportSqlValidator
    {
        SqlValidationResult Validate(string sqlQuery);
    }
}
