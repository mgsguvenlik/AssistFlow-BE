using Business.UnitOfWork;
using Core.Common;
using Core.Enums;

namespace Business.Abstractions
{
    /// <summary>
    /// ASTRON .NET Defaults - Base custom service class(TCD)
    /// </summary>
    public abstract class BaseService
    {
        private readonly IUnitOfWork _unitOfWork;

        protected BaseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Context save & return extra data model object
        /// </summary>
        /// <returns></returns>
        protected async Task<ResponseModel<TEntity>> SaveAsync<TEntity>(TEntity entity) where TEntity : class
        {
            var result = new ResponseModel<TEntity>();
            try
            {
                if (await _unitOfWork.Repository.CompleteAsync() > 0)
                    result = new ResponseModel<TEntity>(true, StatusCode.Success, entity);
                result.Message = "Save operation completed successfully.";
            }
            catch (Exception ex)
            {
                result.Message = ex.GetBaseException().Message;
            }
            return result;
        }


        /// <summary>
        /// Context save
        /// </summary>
        /// <returns></returns>
        protected async Task<ResponseModel> SaveAsync()
        {
            var result = new ResponseModel();
            try
            {
                if (await _unitOfWork.Repository.CompleteAsync() > 0)
                    result.IsSuccess = true;
                result.StatusCode = StatusCode.Success;
                result.Message = "Save operation completed successfully.";
            }
            catch (Exception ex)
            {
                result.Message = ex.GetBaseException().Message;
            }
            return result;
        }
    }
}
