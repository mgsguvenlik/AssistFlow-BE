using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Utilities.Constants;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Dtos.City;
using Model.Dtos.Region;

public class CityService : ICityService
{
    private readonly IUnitOfWork _uow;
    private readonly TypeAdapterConfig _config;

    public CityService(IUnitOfWork uow, TypeAdapterConfig config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<ResponseModel<List<CityGetDto>>> GetAllWithRegionsAsync()
    {
        try
        {
            var query = _uow.Repository.GetQueryable<City>()
                          .AsNoTracking()
                          .OrderBy(c => c.Name);

            var list = await query.ProjectToType<CityGetDto>(_config).ToListAsync();
            return ResponseModel<List<CityGetDto>>.Success(list);
        }
        catch (Exception ex)
        {
            return ResponseModel<List<CityGetDto>>.Fail($"{Messages.UnexpectedError} : {ex.Message}", StatusCode.Error);
        }
    }

    public async Task<ResponseModel<CityGetDto>> GetCityByIdAsync(long id)
    {
        try
        {
            var query = _uow.Repository.GetQueryable<City>()
                          .AsNoTracking()
                          .Where(c => c.Id == id);

            var dto = await query.ProjectToType<CityGetDto>(_config).FirstOrDefaultAsync();

            return dto is null
                ? ResponseModel<CityGetDto>.Fail(Messages.CityNotFound, StatusCode.NotFound)
                : ResponseModel<CityGetDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return ResponseModel<CityGetDto>.Fail($"{Messages.UnexpectedError} : {ex.Message}", StatusCode.Error);
        }
    }

    public async Task<ResponseModel<List<RegionGetDto>>> GetRegionsByCityIdAsync(long cityId)
    {
        try
        {
            var regions = await _uow.Repository.GetQueryable<Region>()
                               .AsNoTracking()
                               .Where(r => r.CityId == cityId)
                               .OrderBy(r => r.Name)
                               .ProjectToType<RegionGetDto>(_config)
                               .ToListAsync();

            return ResponseModel<List<RegionGetDto>>.Success(regions);
        }
        catch (Exception ex)
        {
            return ResponseModel<List<RegionGetDto>>.Fail($"{Messages.UnexpectedError} : {ex.Message}", StatusCode.Error);
        }
    }
}
