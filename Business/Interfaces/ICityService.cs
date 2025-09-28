using Core.Common;
using Model.Dtos.Brand;
using Model.Dtos.City;
using Model.Dtos.Region;

namespace Business.Interfaces
{
    public interface ICityService
    {
        Task<ResponseModel<List<CityGetDto>>> GetAllWithRegionsAsync();
        Task<ResponseModel<CityGetDto>> GetCityByIdAsync(long id);
        Task<ResponseModel<List<RegionGetDto>>> GetRegionsByCityIdAsync(long cityId);
    }
}
