namespace Model.Dtos.WorkingHourPolicy
{
    /// <summary>
    /// Resmi tatilleri senkronize etme sonucu
    /// </summary>
    public class SyncPublicHolidaysDto
    {
        public int Year { get; set; }
        public int TotalFetched { get; set; }
        public int NewAdded { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> AddedHolidays { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}