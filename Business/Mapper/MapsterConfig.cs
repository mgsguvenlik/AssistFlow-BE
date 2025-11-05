using Mapster;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Dtos.Brand;
using Model.Dtos.City;
using Model.Dtos.Configuration;
using Model.Dtos.CurrencyType;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.CustomerGroupProductPrice;
using Model.Dtos.CustomerProductPrice;
using Model.Dtos.CustomerType;
using Model.Dtos.Model;
using Model.Dtos.Product;
using Model.Dtos.ProductType;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Region;
using Model.Dtos.Role;
using Model.Dtos.ServiceType;
using Model.Dtos.SystemType;
using Model.Dtos.User;
using Model.Dtos.UserRole;
using Model.Dtos.WorkFlowDtos.Pricing;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;
using Model.Dtos.WorkFlowDtos.WorkFlowTransition;
using System.Globalization;

namespace Business.Mapper
{
    public class MapsterConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {

            config.Default.MaxDepth(2);


            // ---------------- Brand ----------------
            config.NewConfig<BrandCreateDto, Brand>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Models)
                  .Ignore(d => d.Products);

            config.NewConfig<BrandUpdateDto, Brand>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Models)
                  .Ignore(d => d.Products);

            config.NewConfig<Brand, BrandGetDto>();



            // ---------------- Model ----------------
            config.NewConfig<ModelCreateDto, Model.Concrete.Model>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Brand)
                  .Ignore(d => d.Products);

            config.NewConfig<ModelUpdateDto, Model.Concrete.Model>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Brand)
                  .Ignore(d => d.Products);

            config.NewConfig<Model.Concrete.Model, ModelGetDto>()
                .Map(d => d.Brand, (ur => ur.Brand));


            // ---------------- ProductType ----------------
            config.NewConfig<ProductTypeCreateDto, ProductType>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Products);

            config.NewConfig<ProductTypeUpdateDto, ProductType>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Products);

            config.NewConfig<ProductType, ProductTypeGetDto>();

            // ---------------- CurrencyType ----------------
            config.NewConfig<CurrencyTypeCreateDto, CurrencyType>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Products);

            config.NewConfig<CurrencyTypeUpdateDto, CurrencyType>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Products);

            config.NewConfig<CurrencyType, CurrencyTypeGetDto>();


            // ---------------- CustomerType ----------------
            config.NewConfig<CustomerTypeCreateDto, CustomerType>()
                  .Ignore(d => d.Id);

            config.NewConfig<CustomerTypeUpdateDto, CustomerType>()
                  .IgnoreNullValues(true);

            config.NewConfig<CustomerType, CustomerTypeGetDto>();


            // ---------------- CustomerGroup ----------------
            config.NewConfig<CustomerGroupCreateDto, CustomerGroup>()
                  .Ignore(d => d.Id);
            config.NewConfig<CustomerGroupUpdateDto, CustomerGroup>()
                  .IgnoreNullValues(true);

            config.NewConfig<CustomerGroup, CustomerGroupGetDto>();

            // ---------------- Customer ----------------
            config.NewConfig<CustomerCreateDto, Customer>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.CustomerType);

            config.NewConfig<CustomerUpdateDto, Customer>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.CustomerType);

            config.NewConfig<Customer, CustomerGetDto>();

            // ---------------- ProgressApprover ----------------
            config.NewConfig<ProgressApproverCreateDto, ProgressApprover>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.CustomerGroup);

            config.NewConfig<ProgressApproverUpdateDto, ProgressApprover>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.CustomerGroup);

            config.NewConfig<ProgressApprover, ProgressApproverGetDto>()
                   .Map(d => d.CustomerGroupName, s => s.CustomerGroup != null ? s.CustomerGroup.GroupName : null);


            // ---------------- Role ----------------
            config.NewConfig<RoleCreateDto, Role>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.UserRoles);

            config.NewConfig<RoleUpdateDto, Role>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.UserRoles);

            config.NewConfig<Role, RoleGetDto>()
                   .Map(d => d.Users,
                    s => s.UserRoles.Select(ur => ur.User));

            // ---------------- ServiceType ----------------
            config.NewConfig<ServiceTypeCreateDto, ServiceType>()
                  .Ignore(d => d.Id);

            config.NewConfig<ServiceTypeUpdateDto, ServiceType>()
                  .IgnoreNullValues(true);

            config.NewConfig<ServiceType, ServiceTypeGetDto>();

            // ---------------- SystemType ----------------
            config.NewConfig<SystemTypeCreateDto, SystemType>()
                  .Ignore(d => d.Id);

            config.NewConfig<SystemTypeUpdateDto, SystemType>()
                  .IgnoreNullValues(true);

            config.NewConfig<SystemType, SystemTypeGetDto>();

            // ---------------- User ----------------
            config.NewConfig<UserCreateDto, User>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.UserRoles)
                  .Ignore(d => d.PasswordHash); // hash serviste üretilecek

            config.NewConfig<UserUpdateDto, User>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.UserRoles)
                  .Ignore(d => d.PasswordHash); // NewPassword serviste hash'lenir
            config.NewConfig<User, UserGetDto>()
                  .Map(d => d.Roles,
                       s => s.UserRoles.Select(ur => new RoleGetDto
                       {
                           Id = ur.RoleId,
                           Name = ur.Role != null ? ur.Role.Name : null,
                           Code = ur.Role != null ? ur.Role.Code : null
                       }).ToList()
                  );

            // ---------------- UserRole ----------------
            config.NewConfig<UserRoleCreateDto, UserRole>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.User)
                  .Ignore(d => d.Role);

            config.NewConfig<UserRoleUpdateDto, UserRole>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.User)
                  .Ignore(d => d.Role);

            config.NewConfig<UserRole, UserRoleGetDto>();

            // ---------------- Product ----------------
            config.NewConfig<ProductCreateDto, Product>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Brand)
                  .Ignore(d => d.Model)
                  .Ignore(d => d.CurrencyType)
                  .Ignore(d => d.ProductType);

            config.NewConfig<ProductUpdateDto, Product>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Brand)
                  .Ignore(d => d.Model)
                  .Ignore(d => d.CurrencyType)
                  .Ignore(d => d.ProductType);

            config.NewConfig<Product, ProductGetDto>();

            // Küçük özet
            config.NewConfig<Product, ProductGetDto>();

            config.NewConfig<City, CityGetDto>()
                  .Map(d => d.Regions,
                       s => s.Regions.Select(r => new RegionGetDto
                       {
                           Id = r.Id,
                           Name = r.Name,
                           Code = r.Code,
                           CityId = r.CityId
                       }).ToList());

            config.NewConfig<Region, RegionGetDto>();


            // ---------------- Config ----------------
            config.NewConfig<ConfigurationCreateDto, ServiceType>();
            config.NewConfig<ServiceType, ConfigurationCreateDto>();
            config.NewConfig<ConfigurationUpdateDto, ServiceType>();
            config.NewConfig<ServiceType, ConfigurationUpdateDto>();
            config.NewConfig<ConfigurationGetDto, ServiceType>();
            config.NewConfig<ServiceType, ConfigurationGetDto>();


            //-------------  WorkFlowStep  ----------------
            config.NewConfig<WorkFlowStepCreateDto, WorkFlowStep>()
                    .Ignore(d => d.Id);

            config.NewConfig<WorkFlowStepUpdateDto, WorkFlowStep>()
                  .IgnoreNullValues(true); // partial update

            config.NewConfig<WorkFlowStep, WorkFlowStepGetDto>();


            //-------------  WorkFlow  ----------------
            config.NewConfig<WorkFlowCreateDto, WorkFlow>()
            .Ignore(d => d.Id)
            .Map(d => d.CreatedDate, _ => DateTime.Now)
            .Ignore(d => d.CurrentStep); // FK set edilecek

            config.NewConfig<WorkFlowUpdateDto, WorkFlow>()
                  .IgnoreNullValues(true)
                  .Map(d => d.UpdatedDate, _ => DateTime.Now);

            config.NewConfig<WorkFlow, WorkFlowGetDto>();


            //-------------  ServicesRequest  ----------------
            // --- ServicesRequest: CREATE -> ENTITY ---
            config.NewConfig<ServicesRequestCreateDto, ServicesRequest>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Customer)          // nav
                  .Ignore(d => d.CustomerApprover)  // nav
                  .Ignore(d => d.ServiceType)       // nav
                  .Ignore(d => d.WorkFlowStep);   // nav

            // --- ServicesRequest: UPDATE (partial) -> ENTITY ---
            config.NewConfig<ServicesRequestUpdateDto, ServicesRequest>()
                  .IgnoreNullValues(true)
                  .Ignore(dest => dest.Id)
                  .Ignore(d => d.Customer)          // nav
                  .Ignore(d => d.CustomerApprover)  // nav
                  .Ignore(d => d.ServiceType)       // nav
                  .Ignore(d => d.WorkFlowStep);   // nav

            // --- ServicesRequest: ENTITY -> GET DTO ---
            config.NewConfig<ServicesRequest, ServicesRequestGetDto>()
                  // düz alanlar otomatik eşleşir
                  .Map(d => d.ServicesCostStatusText, s => s.ServicesCostStatus.ToString())
                  .Map(d => d.CustomerName, s => s.Customer != null ? s.Customer.ContactName1 : null)
                  .Map(d => d.CustomerApproverName, s => s.CustomerApprover != null ? s.CustomerApprover.FullName : null)
                  .Map(d => d.ServiceTypeName, s => s.ServiceType != null ? s.ServiceType.Name : null)
                  .Map(d => d.WorkFlowStepName, s => s.WorkFlowStep != null ? s.WorkFlowStep.Name : null);


            // ---------------- Pricing: CustomerGroupProductPrice ----------------
            config.NewConfig<CustomerGroupProductPriceCreateDto, CustomerGroupProductPrice>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.CustomerGroup)
                  .Ignore(d => d.Product);

            config.NewConfig<CustomerGroupProductPriceUpdateDto, CustomerGroupProductPrice>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.CustomerGroup)
                  .Ignore(d => d.Product);

            config.NewConfig<CustomerGroupProductPrice, CustomerGroupProductPriceGetDto>()
                  .Map(d => d.CustomerGroupName, s => s.CustomerGroup != null ? s.CustomerGroup.GroupName : null)
                  .Map(d => d.ProductCode, s => s.Product != null ? s.Product.ProductCode : null)
                  .Map(d => d.ProductDescription, s => s.Product != null ? s.Product.Description : null);

            // ---------------- Pricing: CustomerProductPrice ----------------
            config.NewConfig<CustomerProductPriceCreateDto, CustomerProductPrice>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Customer)
                  .Ignore(d => d.Product);

            config.NewConfig<CustomerProductPriceUpdateDto, CustomerProductPrice>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Customer)
                  .Ignore(d => d.Product);

            config.NewConfig<CustomerProductPrice, CustomerProductPriceGetDto>()
                  .Map(d => d.CustomerName,
                       s => s.Customer != null
                            ? (s.Customer.SubscriberCompany ?? s.Customer.ContactName1)
                            : null)
                  .Map(d => d.ProductCode, s => s.Product != null ? s.Product.ProductCode : null)
                  .Map(d => d.ProductDescription, s => s.Product != null ? s.Product.Description : null);



            // Warehosue Entity -> GetDto
            config.NewConfig<Warehouse, WarehouseGetDto>();

            // Warehosue CreateDto -> Entity
            config.NewConfig<WarehouseCreateDto, Warehouse>(); // koleksiyon başlat


            // Warehosue UpdateDto -> Entity
            config.NewConfig<WarehouseUpdateDto, Warehouse>(); // koleksiyon güncellemesini servis katmanında yapacağız


            //ServicesRequestProduct Dto <-> Entity
            config.NewConfig<ServicesRequestProductCreateDto, ServicesRequestProduct>()
                  .Ignore(d => d.Product);         // nav

            config.NewConfig<ServicesRequestProduct, ServicesRequestProductGetDto>()
                .Map(dest => dest.ProductId, src => src.ProductId)
                .Map(dest => dest.Quantity, src => src.Quantity)
                .Map(dest => dest.PriceCurrency, src => src.Product.PriceCurrency)
                .Map(dest => dest.EffectivePrice, src => src.GetEffectivePrice())
                .Map(dest => dest.ProductPrice, src => src.Product != null ? src.Product.Price : 0m)
                .Map(dest => dest.ProductName, src => src.Product != null ? src.Product.Description : null)
                .Map(dest => dest.ProductCode, src => src.Product != null ? src.Product.ProductCode : null)
                .Map(dest => dest.TotalPrice, src => src.GetTotalEffectivePrice());

            config.NewConfig<ServicesRequestProductUpdateDto, ServicesRequestProduct>();

            // Customer Group 
            TypeAdapterConfig<CustomerGroup, CustomerGroupGetDto>.NewConfig()
                .Map(dest => dest.ParentGroupName, src => src.ParentGroup != null ? src.ParentGroup.GroupName : null)
                .Map(dest => dest.SubGroups, src => src.SubGroups.Adapt<List<CustomerGroupChildDto>>())
                .Map(dest => dest.GroupProductPrices, src => src.GroupProductPrices.Adapt<List<CustomerGroupProductPriceGetDto>>())
                .Map(dest => dest.ProgressApprovers, src => src.ProgressApprovers.Adapt<List<ProgressApproverGetDto>>());



            // ================================
            // TECHNICAL SERVICE
            // ================================
            // Entity -> DTO
            config.NewConfig<TechnicalService, TechnicalServiceGetDto>()
                .Map(d => d.ServicesImages, s => s.ServicesImages)
                .Map(d => d.ServiceRequestFormImages, s => s.ServiceRequestFormImages);

            // DTO -> Entity (tersi)
            config.NewConfig<TechnicalServiceGetDto, TechnicalService>()
                .Map(d => d.ServicesImages, s => s.ServicesImages)
                .Map(d => d.ServiceRequestFormImages, s => s.ServiceRequestFormImages);



            config.NewConfig<TechnicalServiceCreateDto, TechnicalService>()
                .Ignore(dest => dest.Id)
                .Ignore(dest => dest.ServicesImages)
                .Ignore(dest => dest.ServiceRequestFormImages);

            config.NewConfig<TechnicalServiceUpdateDto, TechnicalService>()
                .Ignore(dest => dest.ServicesImages)
                .Ignore(dest => dest.ServiceRequestFormImages);

            // ================================
            // TECHNICAL SERVICE IMAGE
            // ================================
            config.NewConfig<TechnicalServiceImage, TechnicalServiceImageGetDto>();

            // ================================
            // TECHNICAL SERVICE FORM IMAGE
            // ================================
            config.NewConfig<TechnicalServiceFormImage, TechnicalServiceFormImageGetDto>();



            // ---------------- WorkFlowTransition  ----------------
            config.NewConfig<WorkFlowTransition, WorkFlowTransitionGetDto>()
               .Map(dest => dest.FromStepName, src => src.FromStep.Name)
               .Map(dest => dest.ToStepName, src => src.ToStep.Name);

            config.NewConfig<WorkFlowTransitionCreateDto, WorkFlowTransition>();
            config.NewConfig<WorkFlowTransitionUpdateDto, WorkFlowTransition>();

            config.NewConfig<WorkFlowActivityRecorGetDto, WorkFlowActivityRecord>(); 
            config.NewConfig<WorkFlowReviewLog, WorkFlowReviewLogDto>(); 
            config.NewConfig<WorkFlowReviewLogDto, WorkFlowReviewLog>();


            // ---------------- Pricing ----------------
            config.NewConfig<PricingCreateDto, Pricing>()
                  .Ignore(d => d.Id);                 // audit alanları serviste set edilecek

            config.NewConfig<PricingUpdateDto, Pricing>()
                  .IgnoreNullValues(true)             // partial update
                  .Ignore(d => d.Id);                 // Id dışındaki null olmayanları uygula

            // Entity -> GetDto (detay)
            config.NewConfig<Pricing, PricingGetDto>()
                  .Map(d => d.Id, s => s.Id)
                  .Map(d => d.RequestNo, s => s.RequestNo)
                  .Map(d => d.Status, s => s.Status)
                  .Map(d => d.Currency, s => s.Currency)
                  .Map(d => d.Notes, s => s.Notes)
                  .Map(d => d.TotalAmount, s => s.TotalAmount)
                  // audit
                  .Map(d => d.CreatedDate, s => s.CreatedDate)
                  .Map(d => d.CreatedUser, s => s.CreatedUser)
                  .Map(d => d.UpdatedDate, s => s.UpdatedDate)
                  .Map(d => d.UpdatedUser, s => s.UpdatedUser);

        }
    }
}
