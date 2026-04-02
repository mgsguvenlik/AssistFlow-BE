using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class GeographicDistributionDto
    {
        public List<CityStatDto> CityStatistics { get; set; } = new();
        public int TotalCities { get; set; }
        public int TotalDistricts { get; set; }
    }
    
    public class CityStatDto
    {
        public string City { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int ActiveRequests { get; set; }
        public int CompletedRequests { get; set; }
        public decimal TotalRevenueTL { get; set; }
        public decimal TotalRevenueUSD { get; set; }
        
        // Koordinatlar (harita için)
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        
        // İlçe Dağılımı
        public List<DistrictStatDto> Districts { get; set; } = new();
    }
    
    public class DistrictStatDto
    {
        public string District { get; set; } = string.Empty;
        public int RequestCount { get; set; }
    }
}
