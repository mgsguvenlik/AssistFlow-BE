namespace Model.Dtos.WorkingHourPolicy
{
    /// <summary>
    /// Nager.Date API'den gelen response modeli
    /// </summary>
    public class NagerHolidayResponseDto
    {
        public string Date { get; set; } = string.Empty;
        public string LocalName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public bool Fixed { get; set; }
        public bool Global { get; set; }
        public string[]? Counties { get; set; }
        public int? LaunchYear { get; set; }
        public string[]? Types { get; set; }
    }
}