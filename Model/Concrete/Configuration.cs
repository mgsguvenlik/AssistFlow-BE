using Model.Abstractions;

namespace Model.Concrete
{
    public class Configuration : BaseEntity
    {
        public long Id { get; set; }
        public required string Name { get; set; }
        public required string Value { get; set; }
        public string? Description { get; set; }
    }
}
