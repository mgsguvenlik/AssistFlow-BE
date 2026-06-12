using Core.Settings.Abstract;

namespace Core.Settings.Concrete
{
    public class AppSettings : ISettings
    {
        public required string MSSQLConnectionString { get; set; }
        public required string PostgresConnectionString { get; set; }
        public required string Issuer { get; set; }
        public required string Key { get; set; }
        public required string Audience { get; set; }
        public required string OpenidConfiguration { get; set; }
        public required string DbProvider { get; set; }
        public int AccessTokenMinutes { get; set; }
        public required string AppUrl { get; set; }
        public required string FrontUrl { get; set; }
        public  string? FileUrl { get; set; }

        // Manitou Settings
        public required string ManitouBaseUrl { get; set; }
        public string? ManitouUserName { get; set; }
        public required string ManitouPassword { get; set; }
        public string? ManitouAuthenticationType { get; set; }

        public int ManitouCustomerGroupMaxRows { get; set; }
        public int ManitouCustomerMaxRows { get; set; }
        public int ManitouRunEveryMinutes { get; set; }
    }
}
