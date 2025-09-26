namespace Model.Dtos.ServiceType
{
    public class ServiceTypeGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? ContractNumber { get; set; }
    }
}
