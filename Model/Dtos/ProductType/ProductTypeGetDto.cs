namespace Model.Dtos.ProductType
{
    public class ProductTypeGetDto
    {
        public long Id { get; set; }
        public string Type { get; set; } = null!;
        public string? Code { get; set; }
    }
}
