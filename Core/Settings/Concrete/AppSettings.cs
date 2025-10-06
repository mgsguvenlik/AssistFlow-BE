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
    }
}
