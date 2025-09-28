using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.CurrencyType;
using System.Linq.Expressions;

namespace Business.Services
{
    public class CurrencyTypeService
      : CrudServiceBase<CurrencyType, long, CurrencyTypeCreateDto, CurrencyTypeUpdateDto, CurrencyTypeGetDto>,
        ICurrencyTypeService
    {
        public CurrencyTypeService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config)
            : base(uow, mapper, config) { }

        protected override long ReadKey(CurrencyType e) => e.Id;
        protected override Expression<Func<CurrencyType, bool>> KeyPredicate(long id) => x => x.Id == id;

        // Bu DTO ürünleri taşımıyorsa include'a gerek yok
        protected override Func<IQueryable<CurrencyType>, IIncludableQueryable<CurrencyType, object>>? IncludeExpression()
            => null;

        protected override Task<CurrencyType?> ResolveEntityForUpdateAsync(CurrencyTypeUpdateDto dto)
            => _unitOfWork.Repository.GetSingleAsync<CurrencyType>(asNoTracking: false, x => x.Id == dto.Id);
    }
}
