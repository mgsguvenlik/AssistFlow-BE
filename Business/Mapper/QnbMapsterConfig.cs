using Mapster;
using Model.Concrete.Qnb;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbActivityRecord;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbFinalApproval;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbPricing;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequest;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWarehouse;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlow;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlowStep;

namespace Business.Mapper
{
    public static class QnbMapsterConfig
    {
        public static void Register(TypeAdapterConfig config)
        {
            // QnbCustomerForm
            config.NewConfig<QnbCustomerFormCreateDto, QnbCustomerForm>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<QnbCustomerFormUpdateDto, QnbCustomerForm>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<QnbCustomerForm, QnbCustomerFormGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null)
                  .Map(dest => dest.CustomerApproverName, src => src.CustomerApprover != null ? src.CustomerApprover.FullName : null);

            // QnbServicesRequest
            config.NewConfig<QnbServicesRequestCreateDto, QnbServicesRequest>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.IsMailSended)
                  .Ignore(dest => dest.QnbWorkFlowStep);

            config.NewConfig<QnbServicesRequestUpdateDto, QnbServicesRequest>()
                  .Ignore(dest => dest.ServiceTypeRelations)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.QnbWorkFlowStep);

            config.NewConfig<QnbServicesRequest, QnbServicesRequestGetDto>()
                  .Map(dest => dest.ServiceTypes, src => src.ServiceTypeRelations.Select(x => x.ServiceType))
                  .Map(dest => dest.CustomerName, src => src.Customer.SubscriberCompany)
                  .Map(dest => dest.ServiceTypeName, src => src.ServiceType.Name)
                  .Map(dest => dest.WorkFlowStepCode, src => src.QnbWorkFlowStep != null ? src.QnbWorkFlowStep.Code : null)
                  .Map(dest => dest.CustomerApproverName, src => src.CustomerApprover != null ? src.CustomerApprover.FullName : null);

            config.NewConfig<QnbServicesRequest, QnbCustomerForm>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<QnbCustomerForm, QnbServicesRequest>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            // QnbServicesRequestProduct
            config.NewConfig<QnbServicesRequestProductCreateDto, QnbServicesRequestProduct>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CapturedUnitPrice)
                  .Ignore(dest => dest.CapturedCurrency)
                  .Ignore(dest => dest.CapturedTotal)
                  .Ignore(dest => dest.CapturedSource)
                  .Ignore(dest => dest.CapturedAt)
                  .Ignore(dest => dest.IsPriceCaptured);

            config.NewConfig<QnbServicesRequestProductUpdateDto, QnbServicesRequestProduct>();

            config.NewConfig<QnbServicesRequestProduct, QnbServicesRequestProductGetDto>()
                  .Map(dest => dest.ProductName, src => src.Product.Description)
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null)
                  .Map(dest => dest.TotalPrice, src => src.TotalPrice);

            // QnbTechnicalService
            config.NewConfig<QnbTechnicalServiceCreateDto, QnbTechnicalService>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.QnbServiceRequestFormImages)
                  .Ignore(dest => dest.QnbServicesImages);

            config.NewConfig<QnbTechnicalServiceUpdateDto, QnbTechnicalService>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.QnbServiceRequestFormImages)
                  .Ignore(dest => dest.QnbServicesImages);

            config.NewConfig<QnbTechnicalService, QnbTechnicalServiceGetDto>()
                  .Map(dest => dest.ServiceTypeName, src => src.ServiceType != null ? src.ServiceType.Name : null);

            // TS Images
            config.NewConfig<QnbTechnicalServiceImageCreateDto, QnbTechnicalServiceImage>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<QnbTechnicalServiceImage, QnbTechnicalServiceImageGetDto>();

            config.NewConfig<QnbTechnicalServiceFormImageCreateDto, QnbTechnicalServiceFormImage>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<QnbTechnicalServiceFormImage, QnbTechnicalServiceFormImageGetDto>();

            // Pricing
            config.NewConfig<QnbPricingCreateDto, QnbPricing>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<QnbPricingUpdateDto, QnbPricing>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<QnbPricing, QnbPricingGetDto>();

            // FinalApproval
            config.NewConfig<QnbFinalApprovalCreateDto, QnbFinalApproval>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<QnbFinalApprovalUpdateDto, QnbFinalApproval>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<QnbFinalApproval, QnbFinalApprovalGetDto>();

            // Warehouse
            config.NewConfig<QnbWarehouseCreateDto, QnbWarehouse>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<QnbWarehouseUpdateDto, QnbWarehouse>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<QnbWarehouse, QnbWarehouseGetDto>();

            // WorkFlow
            config.NewConfig<QnbWorkFlowCreateDto, QnbWorkFlow>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<QnbWorkFlowUpdateDto, QnbWorkFlow>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<QnbWorkFlow, QnbWorkFlowGetDto>()
                  .Map(dest => dest.CurrentStepCode, src => src.CurrentStep != null ? src.CurrentStep.Code : null)
                  .Map(dest => dest.ApproverTechnicianName, src => src.ApproverTechnician != null ? src.ApproverTechnician.Name : null);

            // WorkFlowStep
            config.NewConfig<QnbWorkFlowStepCreateDto, QnbWorkFlowStep>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<QnbWorkFlowStepUpdateDto, QnbWorkFlowStep>();

            config.NewConfig<QnbWorkFlowStep, QnbWorkFlowStepGetDto>();

            // ActivityRecord
            config.NewConfig<QnbWorkFlowActivityRecord, QnbWorkFlowActivityRecordGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null);

            // Archive
            config.NewConfig<QnbWorkFlowArchive, QnbWorkFlowArchiveGetDto>();

            // ReviewLog
            config.NewConfig<QnbWorkFlowReviewLogDto, QnbWorkFlowReviewLog>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.CreatedUser);

            config.NewConfig<QnbWorkFlowReviewLog, QnbWorkFlowReviewLogDto>();
        }
    }
}