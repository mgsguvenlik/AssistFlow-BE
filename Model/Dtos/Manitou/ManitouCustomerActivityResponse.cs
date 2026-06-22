using System.Text.Json.Serialization;

namespace Model.Dtos.Manitou
{
    public sealed class ManitouCustomerActivityResponse
    {
        [JsonPropertyName("days")]
        public int Days { get; set; }

        [JsonPropertyName("details")]
        public List<ManitouCustomerActivityDetail> Details { get; set; } = new();
    }

    
}