using Mapster;
using Model.Concrete.Ykb;
using Model.Dtos.WorkFlowDtos.YkbDtos.ActivityRecord;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbPricing;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequest;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWarehouse;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlow;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep;

namespace Business.Mapper
{
    public static class YkbMapsterConfig
    {
        public static void Register(TypeAdapterConfig config)
        {
            // YkbCustomerForm
            config.NewConfig<YkbCustomerFormCreateDto, YkbCustomerForm>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<YkbCustomerFormUpdateDto, YkbCustomerForm>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<YkbCustomerForm, YkbCustomerFormGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null)
                  .Map(dest => dest.CustomerApproverName, src => src.CustomerApprover != null ? src.CustomerApprover.FullName : null);

            // YkbServicesRequest
            config.NewConfig<YkbServicesRequestCreateDto, YkbServicesRequest>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.IsMailSended)
                  .Ignore(dest => dest.YkbWorkFlowStep);

            config.NewConfig<YkbServicesRequestUpdateDto, YkbServicesRequest>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.YkbWorkFlowStep);

            config.NewConfig<YkbServicesRequest, YkbServicesRequestGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer.SubscriberCompany)
                  .Map(dest => dest.ServiceTypeName, src => src.ServiceType.Name)
                  .Map(dest => dest.WorkFlowStepCode, src => src.YkbWorkFlowStep != null ? src.YkbWorkFlowStep.Code : null)
                  .Map(dest => dest.CustomerApproverName, src => src.CustomerApprover != null ? src.CustomerApprover.FullName : null);


            config.NewConfig<YkbServicesRequest, YkbCustomerForm>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<YkbCustomerForm, YkbServicesRequest>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            // YkbServicesRequestProduct
            config.NewConfig<YkbServicesRequestProductCreateDto, YkbServicesRequestProduct>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CapturedUnitPrice)
                  .Ignore(dest => dest.CapturedCurrency)
                  .Ignore(dest => dest.CapturedTotal)
                  .Ignore(dest => dest.CapturedSource)
                  .Ignore(dest => dest.CapturedAt)
                  .Ignore(dest => dest.IsPriceCaptured);

            config.NewConfig<YkbServicesRequestProductUpdateDto, YkbServicesRequestProduct>();

            config.NewConfig<YkbServicesRequestProduct, YkbServicesRequestProductGetDto>()
                  .Map(dest => dest.ProductName, src => src.Product.Description)
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null)
                  .Map(dest => dest.TotalPrice, src => src.TotalPrice);

            // YkbTechnicalService
            config.NewConfig<YkbTechnicalServiceCreateDto, YkbTechnicalService>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.YkbServiceRequestFormImages)
                  .Ignore(dest => dest.YkbServicesImages);

            config.NewConfig<YkbTechnicalServiceUpdateDto, YkbTechnicalService>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.YkbServiceRequestFormImages)
                  .Ignore(dest => dest.YkbServicesImages);

            config.NewConfig<YkbTechnicalService, YkbTechnicalServiceGetDto>()
                  .Map(dest => dest.ServiceTypeName, src => src.ServiceType != null ? src.ServiceType.Name : null);

            // TS Images
            config.NewConfig<YkbTechnicalServiceImageCreateDto, YkbTechnicalServiceImage>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<YkbTechnicalServiceImage, YkbTechnicalServiceImageGetDto>();

            config.NewConfig<YkbTechnicalServiceFormImageCreateDto, YkbTechnicalServiceFormImage>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<YkbTechnicalServiceFormImage, YkbTechnicalServiceFormImageGetDto>();

            // Pricing
            config.NewConfig<YkbPricingCreateDto, YkbPricing>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<YkbPricingUpdateDto, YkbPricing>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<YkbPricing, YkbPricingGetDto>();

            // FinalApproval
            config.NewConfig<YkbFinalApprovalCreateDto, YkbFinalApproval>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<YkbFinalApprovalUpdateDto, YkbFinalApproval>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<YkbFinalApproval, YkbFinalApprovalGetDto>();

            // Warehouse
            config.NewConfig<YkbWarehouseCreateDto, YkbWarehouse>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<YkbWarehouseUpdateDto, YkbWarehouse>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<YkbWarehouse, YkbWarehouseGetDto>();

            // WorkFlow
            config.NewConfig<YkbWorkFlowCreateDto, YkbWorkFlow>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<YkbWorkFlowUpdateDto, YkbWorkFlow>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<YkbWorkFlow, YkbWorkFlowGetDto>()
                  .Map(dest => dest.CurrentStepCode, src => src.CurrentStep != null ? src.CurrentStep.Code : null)
                  .Map(dest => dest.ApproverTechnicianName, src => src.ApproverTechnician != null ? src.ApproverTechnician.TechnicianName : null);

            // WorkFlowStep
            config.NewConfig<YkbWorkFlowStepCreateDto, YkbWorkFlowStep>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<YkbWorkFlowStepUpdateDto, YkbWorkFlowStep>();

            config.NewConfig<YkbWorkFlowStep, YkbWorkFlowStepGetDto>();

            // ActivityRecord
            config.NewConfig<YkbWorkFlowActivityRecord, YkbWorkFlowActivityRecordGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null);

            // Archive
            config.NewConfig<YkbWorkFlowArchive, YkbWorkFlowArchiveGetDto>();

            // ReviewLog
            config.NewConfig<YkbWorkFlowReviewLogDto, YkbWorkFlowReviewLog>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.CreatedUser);

            config.NewConfig<YkbWorkFlowReviewLog, YkbWorkFlowReviewLogDto>();
        }
    }
}
