using Mapster;
using Model.Concrete.Ekb;
using Model.Dtos.WorkFlowDtos.EkbDtos.ActivityRecord;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbArchive;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbCustomerForm;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbFinalApproval;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbPricing;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequest;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalService;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWarehouse;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlow;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlowStep;

namespace Business.Mapper
{
    public class EkbMapsterConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // EkbCustomerForm
            config.NewConfig<EkbCustomerFormCreateDto, EkbCustomerForm>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<EkbCustomerFormUpdateDto, EkbCustomerForm>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<EkbCustomerForm, EkbCustomerFormGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null)
                  .Map(dest => dest.CustomerApproverName, src => src.CustomerApprover != null ? src.CustomerApprover.FullName : null);

            // EkbServicesRequest
            config.NewConfig<EkbServicesRequestCreateDto, EkbServicesRequest>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.IsMailSended)
                  .Ignore(dest => dest.EkbWorkFlowStep);

            config.NewConfig<EkbServicesRequestUpdateDto, EkbServicesRequest>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.EkbWorkFlowStep);

            config.NewConfig<EkbServicesRequest, EkbServicesRequestGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer.SubscriberCompany)
                  .Map(dest => dest.ServiceTypeName, src => src.ServiceType.Name)
                  .Map(dest => dest.WorkFlowStepCode, src => src.EkbWorkFlowStep != null ? src.EkbWorkFlowStep.Code : null)
                  .Map(dest => dest.CustomerApproverName, src => src.CustomerApprover != null ? src.CustomerApprover.FullName : null);


            config.NewConfig<EkbServicesRequest, EkbCustomerForm>()
                  .Map(dest => dest.ServicesDate, src => src.ServicesDate.DateTime)
                  .Map(dest => dest.PlannedCompletionDate, src => src.PlannedCompletionDate.HasValue
                      ? src.PlannedCompletionDate.Value.DateTime : (DateTime?)null)
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<EkbCustomerForm, EkbServicesRequest>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            // EkbServicesRequestProduct
            config.NewConfig<EkbServicesRequestProductCreateDto, EkbServicesRequestProduct>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CapturedUnitPrice)
                  .Ignore(dest => dest.CapturedCurrency)
                  .Ignore(dest => dest.CapturedTotal)
                  .Ignore(dest => dest.CapturedSource)
                  .Ignore(dest => dest.CapturedAt)
                  .Ignore(dest => dest.IsPriceCaptured);

            config.NewConfig<EkbServicesRequestProductUpdateDto, EkbServicesRequestProduct>();

            config.NewConfig<EkbServicesRequestProduct, EkbServicesRequestProductGetDto>()
                  .Map(dest => dest.ProductName, src => src.Product.Description)
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null)
                  .Map(dest => dest.TotalPrice, src => src.TotalPrice);

            // EkbTechnicalService
            config.NewConfig<EkbTechnicalServiceCreateDto, EkbTechnicalService>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.EkbServiceRequestFormImages)
                  .Ignore(dest => dest.EkbServicesImages);

            config.NewConfig<EkbTechnicalServiceUpdateDto, EkbTechnicalService>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.EkbServiceRequestFormImages)
                  .Ignore(dest => dest.EkbServicesImages);

            config.NewConfig<EkbTechnicalService, EkbTechnicalServiceGetDto>()
                    .Map(dest => dest.ServiceTypeName,
                         src => src.ServiceType != null
                             ? src.ServiceType.Name
                             : null)
                    .Map(dest => dest.ServiceRequestFormImages,
                         src => src.EkbServiceRequestFormImages)
                    .Map(dest => dest.ServicesImages,
                         src => src.EkbServicesImages);

            config.NewConfig<EkbTechnicalServiceImage, EkbTechnicalServiceImageGetDto>();

            config.NewConfig<EkbTechnicalServiceFormImage, EkbTechnicalServiceFormImageGetDto>();

            // Pricing
            config.NewConfig<EkbPricingCreateDto, EkbPricing>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<EkbPricingUpdateDto, EkbPricing>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<EkbPricing, EkbPricingGetDto>();

            // FinalApproval
            config.NewConfig<EkbFinalApprovalCreateDto, EkbFinalApproval>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<EkbFinalApprovalUpdateDto, EkbFinalApproval>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<EkbFinalApproval, EkbFinalApprovalGetDto>();

            // Warehouse
            config.NewConfig<EkbWarehouseCreateDto, EkbWarehouse>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<EkbWarehouseUpdateDto, EkbWarehouse>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<EkbWarehouse, EkbWarehouseGetDto>();

            // WorkFlow
            config.NewConfig<EkbWorkFlowCreateDto, EkbWorkFlow>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.UpdatedUser)
                  .Ignore(dest => dest.UpdatedDate);

            config.NewConfig<EkbWorkFlowUpdateDto, EkbWorkFlow>()
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.CreatedDate);

            config.NewConfig<EkbWorkFlow, EkbWorkFlowGetDto>()
                  .Map(dest => dest.CurrentStepCode, src => src.CurrentStep != null ? src.CurrentStep.Code : null)
                  .Map(dest => dest.ApproverTechnicianName, src => src.ApproverTechnician != null ? src.ApproverTechnician.Name : null);

            // WorkFlowStep
            config.NewConfig<EkbWorkFlowStepCreateDto, EkbWorkFlowStep>()
                  .Ignore(dest => dest.Id);

            config.NewConfig<EkbWorkFlowStepUpdateDto, EkbWorkFlowStep>();

            config.NewConfig<EkbWorkFlowStep, EkbWorkFlowStepGetDto>();

            // ActivityRecord
            config.NewConfig<EkbWorkFlowActivityRecord, EkbWorkFlowActivityRecordGetDto>()
                  .Map(dest => dest.CustomerName, src => src.Customer != null ? src.Customer.SubscriberCompany : null);

            // Archive
            config.NewConfig<EkbWorkFlowArchive, EkbWorkFlowArchiveGetDto>();

            // ReviewLog
            config.NewConfig<EkbWorkFlowReviewLogDto, EkbWorkFlowReviewLog>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.CreatedUser);

            config.NewConfig<EkbWorkFlowReviewLog, EkbWorkFlowReviewLogDto>();
        }
    }
}
