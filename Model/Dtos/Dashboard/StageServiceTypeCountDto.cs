namespace Model.Dtos.Dashboard;

public sealed class StageServiceTypeCountDto
{
    public string StageCode { get; set; } = string.Empty;
    public long? ServiceTypeId { get; set; }
    public string ServiceTypeName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class DashboardServiceTypeOptionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
