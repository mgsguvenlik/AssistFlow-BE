using Core.Enums;

namespace Model.Dtos.Dashboard
{
    public class YkbTechnicalServiceStatusCountDto
    {
        public TechnicalServiceStatus ServicesStatus { get; set; }

        public string ServicesStatusName { get; set; } = string.Empty;

        public int RecordCount { get; set; }
    }
}
