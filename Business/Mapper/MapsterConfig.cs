using Core.Enums;
using Core.Enums.Crm;
using Mapster;
using Model.Concrete;
using Model.Concrete.Crm;
using Model.Concrete.WorkFlows;
using Model.Dtos.Brand;
using Model.Dtos.City;
using Model.Dtos.Configuration;
using Model.Dtos.Crm.PurchaseAttachment;
using Model.Dtos.Crm.PurchaseRequest;
using Model.Dtos.Crm.PurchaseRequestAction;
using Model.Dtos.Crm.PurchaseRequestHistory;
using Model.Dtos.Crm.PurchaseRequestItem;
using Model.Dtos.Crm.PurchaseRequestStep;
using Model.Dtos.Crm.PurchaseRequestTask;
using Model.Dtos.CurrencyType;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.CustomerGroupProductPrice;
using Model.Dtos.CustomerProductPrice;
using Model.Dtos.CustomerSystem;
using Model.Dtos.CustomerSystemAssignment;
using Model.Dtos.CustomerType;
using Model.Dtos.MailOutbox;
using Model.Dtos.Menu;
using Model.Dtos.Model;
using Model.Dtos.Notification;
using Model.Dtos.Product;
using Model.Dtos.ProductType;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Region;
using Model.Dtos.Role;
using Model.Dtos.ServiceType;
using Model.Dtos.SystemType;
using Model.Dtos.Tenant;
using Model.Dtos.TenantProductPrice;
using Model.Dtos.User;
using Model.Dtos.UserFeedbackDtos;
using Model.Dtos.UserRole;
using Model.Dtos.WorkFlowDtos.FinalApproval;
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
using Model.Dtos.WorkingHourPolicy;
using Model.Dtos.WorkOrderType;

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
                  .Ignore(d => d.CustomerType)
                  .Ignore(d => d.CustomerGroup)
                  .Ignore(d => d.CustomerProductPrices)
                  // eski: .Ignore(d => d.CustomerSystems)
                  .Ignore(d => d.CustomerSystemAssignments);  // 🔹 yeni ara tablo nav

            config.NewConfig<CustomerUpdateDto, Customer>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.CustomerType)
                  .Ignore(d => d.CustomerGroup)
                  .Ignore(d => d.CustomerProductPrices)
                  // eski: .Ignore(d => d.CustomerSystems)
                  .Ignore(d => d.CustomerSystemAssignments);  // 🔹 yeni ara tablo nav

            // Burada CustomerGetDto.Systems’in tipine göre mapping yapıyoruz.
            // Eğer Systems = List<CustomerSystemAssignmentGetDto> ise:
            config.NewConfig<Customer, CustomerGetDto>()
                  .Map(d => d.Systems, s => s.CustomerSystemAssignments);

            // ---------------- CustomerSystem ----------------
            config.NewConfig<CustomerSystemCreateDto, CustomerSystem>()
                  .Ignore(d => d.Id)
                  // eski: .Ignore(d => d.Customers)
                  .Ignore(d => d.CustomerSystemAssignments);   // 🔹 yeni ara tablo nav

            config.NewConfig<CustomerSystemUpdateDto, CustomerSystem>()
                  .IgnoreNullValues(true)
                  // eski: .Ignore(d => d.Customers)
                  .Ignore(d => d.CustomerSystemAssignments);   // 🔹 yeni ara tablo nav

            config.NewConfig<CustomerSystem, CustomerSystemGetDto>();

            // ---------------- CustomerSystemAssignment ----------------
            // Entity -> GetDto
            config.NewConfig<CustomerSystemAssignment, CustomerSystemAssignmentGetDto>()
                  .Map(d => d.CustomerName, s => s.Customer.SubscriberCompany)
                  .Map(d => d.CustomerShortCode, s => s.Customer.CustomerShortCode)
                  .Map(d => d.SystemName, s => s.CustomerSystem.Name)
                  .Map(d => d.SystemCode, s => s.CustomerSystem.Code);

            // Create / Update DTO -> Entity
            config.NewConfig<CustomerSystemAssignmentCreateDto, CustomerSystemAssignment>();
            config.NewConfig<CustomerSystemAssignmentUpdateDto, CustomerSystemAssignment>()
                  .IgnoreNullValues(true);




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

            // Role -> RoleGetDto
            config.ForType<Role, RoleGetDto>()
                // Users: UserRole → User
                .Map(dest => dest.Users,
                     src => src.UserRoles.Select(ur => ur.User).Where(u => u != null))

                // Menus: MenuRole → Menu + izinler
                .Map(dest => dest.Menus,
                     src => src.MenuRoles.Select(mr => new MenuWithPermissionsDto
                     {
                         Id = mr.Menu != null ? mr.Menu.Id : 0,
                         Name = mr.Menu != null ? mr.Menu.Name : string.Empty,
                         CanView = mr.HasView,
                         CanEdit = mr.HasEdit
                     }));


            // MenuRole -> MenuWithPermissionsDto
            TypeAdapterConfig<Model.Concrete.MenuRole, Model.Dtos.Menu.MenuWithPermissionsDto>
                .NewConfig()
                .Map(d => d.Id, s => s.MenuId)          // veya s.Menu!.Id; kolon eşlemesine göre
                .Map(d => d.Name, s => s.Menu!.Name)
                .Map(d => d.CanView, s => s.HasView)
                .Map(d => d.CanEdit, s => s.HasEdit);
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
                    // 🔹 Tenant alanları
                    .Map(d => d.TenantId,
                         s => s.TenantId ?? 0) // DTO long, entity long? olduğu için null gelirse 0 veriyoruz
                    .Map(d => d.TenantCode,
                         s => s.Tenant != null ? s.Tenant.Code : string.Empty)
                    .Map(d => d.TenantName,s => s.Tenant != null ? s.Tenant.Name : string.Empty)
                    .Map(d => d.IsTechnicalServiceTestEnabled, s => s.Tenant != null ? s.Tenant.IsTechnicalServiceTestEnabled : false)
                    // 🔹 Diğer basit alanlar (istersen bunları Mapster’a da bırakabilirsin)
                    .Map(d => d.TechnicianCode, s => s.TechnicianCode)
                    .Map(d => d.TechnicianCompany, s => s.TechnicianCompany)
                    .Map(d => d.TechnicianAddress, s => s.TechnicianAddress)
                    .Map(d => d.City, s => s.City)
                    .Map(d => d.District, s => s.District)
                    .Map(d => d.TechnicianName, s => s.TechnicianName)
                    .Map(d => d.TechnicianPhone, s => s.TechnicianPhone)
                    .Map(d => d.TechnicianEmail, s => s.TechnicianEmail)
                    .Map(d => d.IsActive, s => s.IsActive)

                    // 🔹 Roller
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
                  .Ignore(d => d.WorkFlowStep)
                  .Ignore(d => d.ServicesRequestWorkOrderTypes);

            // --- ServicesRequest: UPDATE (partial) -> ENTITY ---
            config.NewConfig<ServicesRequestUpdateDto, ServicesRequest>()
                  .IgnoreNullValues(true)
                  .Ignore(dest => dest.Id)
                  .Ignore(d => d.Customer)          // nav
                  .Ignore(d => d.CustomerApprover)  // nav
                  .Ignore(d => d.ServiceType)       // nav
                  .Ignore(d => d.WorkFlowStep)   // nav
                  .Ignore(d => d.ServicesRequestWorkOrderTypes);

            // --- ServicesRequest: ENTITY -> GET DTO ---
            config.NewConfig<ServicesRequest, ServicesRequestGetDto>()
                  // düz alanlar otomatik eşleşir
                  .Map(d => d.ServicesCostStatusText, s => s.ServicesCostStatus.ToString())
                  .Map(d => d.CustomerName, s => s.Customer != null ? s.Customer.ContactName1 : null)
                  .Map(d => d.CustomerApproverName, s => s.CustomerApprover != null ? s.CustomerApprover.FullName : null)
                  .Map(d => d.ServiceTypeName, s => s.ServiceType != null ? s.ServiceType.Name : null)
                  .Map(d => d.WorkFlowStepName, s => s.WorkFlowStep != null ? s.WorkFlowStep.Name : null)
                  .Map(d => d.WorkOrderTypes,
                     s => s.ServicesRequestWorkOrderTypes
                           .Where(x => x.WorkOrderType != null)
                           .Select(x => new WorkOrderTypeGetDto
                           {
                               Id = x.WorkOrderTypeId,
                               Name = x.WorkOrderType.Name,
                               Code = x.WorkOrderType.Code
                           })
                           .ToList());


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
                  //.Map(d => d.TotalAmount, s => s.TotalAmount)
                  // audit
                  .Map(d => d.CreatedDate, s => s.CreatedDate)
                  .Map(d => d.CreatedUser, s => s.CreatedUser)
                  .Map(d => d.UpdatedDate, s => s.UpdatedDate)
                  .Map(d => d.UpdatedUser, s => s.UpdatedUser);



            // ---------------- MailOutbox ----------------
            config.NewConfig<MailOutboxCreateDto, MailOutbox>()
                  .Ignore(d => d.Id)
                  .Map(d => d.Status, _ => MailOutboxStatus.Pending)
                  .Map(d => d.TryCount, _ => 0)
                  .Map(d => d.MaxTry, s => s.MaxTry ?? 5)
                  .Map(d => d.CreatedDate, _ => DateTime.Now)
                  .Ignore(d => d.UpdatedDate)
                  .Ignore(d => d.UpdatedUser);

            config.NewConfig<MailOutboxUpdateDto, MailOutbox>()
                  .IgnoreNullValues(true);

            config.NewConfig<MailOutbox, MailOutboxGetDto>()
                  .Map(d => d.Status, s => (int)s.Status);


            // Entity -> GetDto
            config.NewConfig<FinalApproval, FinalApprovalGetDto>();


            // Menu
            config.NewConfig<Model.Dtos.Menu.MenuCreateDto, Model.Concrete.Menu>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.MenuRoles);

            config.NewConfig<Model.Dtos.Menu.MenuUpdateDto, Model.Concrete.Menu>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.MenuRoles);

            config.NewConfig<Model.Concrete.Menu, Model.Dtos.Menu.MenuGetDto>();

            // MenuRole
            config.NewConfig<Model.Dtos.MenuRole.MenuRoleCreateDto, Model.Concrete.MenuRole>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Menu)
                  .Ignore(d => d.Role);

            config.NewConfig<Model.Dtos.MenuRole.MenuRoleUpdateDto, Model.Concrete.MenuRole>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Menu)
                  .Ignore(d => d.Role);

            config.NewConfig<Model.Concrete.MenuRole, Model.Dtos.MenuRole.MenuRoleGetDto>()
                  .Map(d => d.MenuName, s => s.Menu != null ? s.Menu.Name : null)
                  .Map(d => d.RoleName, s => s.Role != null ? s.Role.Name : null);



            config.NewConfig<NotificationCreateDto, Notification>()
                 .Ignore(d => d.Id)
                 .Map(d => d.CreatedDate, _ => DateTime.Now)
                 .Map(d => d.IsRead, _ => false)
                 .Ignore(d => d.ReadAt);

            config.NewConfig<Notification, NotificationGetDto>()
                  .Map(d => d.Type, s => (int)s.Type);

            config.NewConfig<Tenant, TenantGetDto>();

            config.NewConfig<TenantCreateDto, Tenant>()
                  .Ignore(dest => dest.Id)
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.CreatedUser)
                  .Ignore(dest => dest.UpdatedDate)
                  .Ignore(dest => dest.UpdatedUser);
            config.NewConfig<TenantUpdateDto, Tenant>()
                  .Ignore(dest => dest.CreatedDate)
                  .Ignore(dest => dest.CreatedUser);


            // ---------------- UserFeedback ----------------
            config.NewConfig<CreateUserFeedbackDto, UserFeedback>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Status)              // Servis'te Created olarak set edilir
                  .Ignore(d => d.Priority)            // Servis'te default değer verilir
                  .Ignore(d => d.AdminResponse)
                  .Ignore(d => d.ResponseDate)
                  .Ignore(d => d.RespondedBy)
                  .Ignore(d => d.CompletedDate)
                  .Ignore(d => d.UserAgent)           // Servis'te set edilir
                  .Ignore(d => d.AttachmentUrls)      // Servis'te JSON'a cevrilir
                  .Ignore(d => d.CreatedDate)
                  .Ignore(d => d.CreatedUser)
                  .Ignore(d => d.UpdatedDate)
                  .Ignore(d => d.UpdatedUser)
                  .Ignore(d => d.IsDeleted);

            config.NewConfig<UpdateFeedbackStatusDto, UserFeedback>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Title)
                  .Ignore(d => d.Description)
                  .Ignore(d => d.FeedbackType)
                  .Ignore(d => d.RelatedUrl)
                  .Ignore(d => d.UserAgent)
                  .Ignore(d => d.AttachmentUrls)
                  .Ignore(d => d.CreatedDate)
                  .Ignore(d => d.CreatedUser)
                  .Ignore(d => d.UpdatedDate)         // Servis'te set edilir
                  .Ignore(d => d.UpdatedUser)         // Servis'te set edilir
                  .Ignore(d => d.IsDeleted);

            config.NewConfig<UserFeedback, UserFeedbackDto>()
                  .Map(d => d.FeedbackTypeText, s => GetFeedbackTypeText(s.FeedbackType))
                  .Map(d => d.StatusText, s => GetStatusText(s.Status))
                  .Map(d => d.AttachmentUrls, s => DeserializeAttachmentUrls(s.AttachmentUrls))
                  .Map(d => d.CreatedUserName, s => (string?)null)     // Servis'te doldurulur
                  .Map(d => d.RespondedByName, s => (string?)null);    // Servis'te doldurulur


            // ---------------- TenantProductPrice ----------------
            config.NewConfig<TenantProductPriceCreateDto, TenantProductPrice>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Tenant)
                  .Ignore(d => d.Product);

            config.NewConfig<TenantProductPriceUpdateDto, TenantProductPrice>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Tenant)
                  .Ignore(d => d.Product);

            config.NewConfig<TenantProductPrice, TenantProductPriceGetDto>()
                  .Map(d => d.TenantName, s => s.Tenant != null ? s.Tenant.Name : null)
                  .Map(d => d.TenantCode, s => s.Tenant != null ? s.Tenant.Code : null)
                  .Map(d => d.ProductCode, s => s.Product != null ? s.Product.ProductCode : null)
                  .Map(d => d.ProductDescription, s => s.Product != null ? s.Product.Description : null);

            // ---------------- WorkingHourPolicy ----------------
            config.NewConfig<WorkingHourPolicyCreateDto, WorkingHourPolicy>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Tenant)
                  .Ignore(d => d.HolidayTypes)
                  .Ignore(d => d.CreatedDate)
                  .Ignore(d => d.CreatedUser)
                  .Ignore(d => d.UpdatedDate)
                  .Ignore(d => d.UpdatedUser)
                  .Ignore(d => d.IsDeleted);

            config.NewConfig<WorkingHourPolicyUpdateDto, WorkingHourPolicy>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Name)
                  .Ignore(d => d.PolicyType)
                  .Ignore(d => d.SpecificDate)
                  .Ignore(d => d.Year)
                  .Ignore(d => d.DayOfWeek)
                  .Ignore(d => d.CountryCode)
                  .Ignore(d => d.IsPublicHoliday)
                  .Ignore(d => d.HolidayTypes)
                  .Ignore(d => d.Tenant)
                  .Ignore(d => d.CreatedDate)
                  .Ignore(d => d.CreatedUser)
                  .Ignore(d => d.UpdatedDate)
                  .Ignore(d => d.UpdatedUser)
                  .Ignore(d => d.IsDeleted);

            config.NewConfig<WorkingHourPolicy, WorkingHourPolicyGetDto>()
                  .Map(d => d.PolicyTypeText, s => GetPolicyTypeText(s.PolicyType))
                  .Map(d => d.DayOfWeekText, s => s.DayOfWeek.HasValue ? GetDayOfWeekText(s.DayOfWeek.Value) : null);



            // ---------------- WorkOrderType ----------------
            config.NewConfig<WorkOrderTypeCreateDto, WorkOrderType>()
                  .Ignore(d => d.Id);

            config.NewConfig<WorkOrderTypeUpdateDto, WorkOrderType>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id);

            config.NewConfig<WorkOrderType, WorkOrderTypeGetDto>();


            #region CRM

            // =======================================================
            // PurchaseRequest
            // =======================================================

            config.NewConfig<PurchaseRequestCreateDto, PurchaseRequest>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.RequestNo)
                  .Ignore(d => d.RequesterUserId)
                  .Ignore(d => d.Status)
                  .Ignore(d => d.CurrentStepId)
                  .Ignore(d => d.ClosedDate)

                  // Navigations
                  .Ignore(d => d.Tenant)
                  .Ignore(d => d.RequesterUser)
                  .Ignore(d => d.ManagerUser)
                  .Ignore(d => d.Customer)
                  .Ignore(d => d.SystemType)
                  .Ignore(d => d.CurrentStep)
                  .Ignore(d => d.Items)
                  .Ignore(d => d.Tasks)
                  .Ignore(d => d.Histories)
                  .Ignore(d => d.Attachments);


            // Partial update.
            // Workflow tarafından yönetilecek alanların update DTO üzerinden
            // değiştirilmesine izin vermiyoruz.
            config.NewConfig<PurchaseRequestUpdateDto, PurchaseRequest>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id)
                  .Ignore(d => d.RequestNo)
                  .Ignore(d => d.RequesterUserId)
                  .Ignore(d => d.Status)
                  .Ignore(d => d.CurrentStepId)
                  .Ignore(d => d.ClosedDate)

                  // Navigations
                  .Ignore(d => d.Tenant)
                  .Ignore(d => d.RequesterUser)
                  .Ignore(d => d.ManagerUser)
                  .Ignore(d => d.Customer)
                  .Ignore(d => d.SystemType)
                  .Ignore(d => d.CurrentStep)
                  .Ignore(d => d.Items)
                  .Ignore(d => d.Tasks)
                  .Ignore(d => d.Histories)
                  .Ignore(d => d.Attachments);


            config.NewConfig<PurchaseRequest, PurchaseRequestGetDto>()
                  .Map(d => d.TenantName,
                       s => s.Tenant != null
                           ? s.Tenant.Name
                           : null)

                  .Map(d => d.RequesterUserName,
                       s => s.RequesterUser != null
                           ? s.RequesterUser.TechnicianName
                           : null)

                  .Map(d => d.ManagerUserName,
                       s => s.ManagerUser != null
                           ? s.ManagerUser.TechnicianName
                           : null)

                  .Map(d => d.CustomerName,
                       s => s.Customer != null
                           ? (s.Customer.SubscriberCompany ?? s.Customer.ContactName1)
                           : null)

                  .Map(d => d.SystemTypeName,
                       s => s.SystemType != null
                           ? s.SystemType.Name
                           : null)

                  .Map(d => d.CurrentStepCode,
                       s => s.CurrentStep != null
                           ? s.CurrentStep.Code
                           : null)

                  .Map(d => d.CurrentStepName,
                       s => s.CurrentStep != null
                           ? s.CurrentStep.Name
                           : null)

                  .Map(d => d.RequestTypeName,
                       s => s.RequestType == PurchaseRequestType.NormalPurchase
                           ? "Normal Satın Alma"
                           : s.RequestType == PurchaseRequestType.ResearchAndOffer
                               ? "Araştırma ve Teklif"
                               : "Bilinmiyor")

                  .Map(d => d.StatusName,
                       s => s.Status == PurchaseRequestStatus.Draft
                           ? "Taslak"
                           : s.Status == PurchaseRequestStatus.InProgress
                               ? "Devam Ediyor"
                               : s.Status == PurchaseRequestStatus.RevisionRequired
                                   ? "Revizyon Bekliyor"
                                   : s.Status == PurchaseRequestStatus.Completed
                                       ? "Tamamlandı"
                                       : s.Status == PurchaseRequestStatus.Rejected
                                           ? "Reddedildi"
                                           : s.Status == PurchaseRequestStatus.Cancelled
                                               ? "İptal Edildi"
                                               : "Bilinmiyor");


            // =======================================================
            // PurchaseRequest Detail
            // =======================================================
            //
            // AvailableActions özellikle burada maplenmiyor.
            // Çünkü o alan current user / role / aktif task kontrolü yapıldıktan sonra
            // service katmanında oluşturulacak.
            //
            config.NewConfig<PurchaseRequest, PurchaseRequestDetailDto>()
                  .Map(d => d.TenantName,
                       s => s.Tenant != null
                           ? s.Tenant.Name
                           : null)

                  .Map(d => d.RequesterUserName,
                       s => s.RequesterUser != null
                           ? s.RequesterUser.TechnicianName
                           : null)

                  .Map(d => d.ManagerUserName,
                       s => s.ManagerUser != null
                           ? s.ManagerUser.TechnicianName
                           : null)

                  .Map(d => d.CustomerName,
                       s => s.Customer != null
                           ? (s.Customer.SubscriberCompany ?? s.Customer.ContactName1)
                           : null)

                  .Map(d => d.SystemTypeName,
                       s => s.SystemType != null
                           ? s.SystemType.Name
                           : null)

                  .Map(d => d.CurrentStepCode,
                       s => s.CurrentStep != null
                           ? s.CurrentStep.Code
                           : null)

                  .Map(d => d.CurrentStepName,
                       s => s.CurrentStep != null
                           ? s.CurrentStep.Name
                           : null)

                  .Map(d => d.RequestTypeName,
                       s => s.RequestType == PurchaseRequestType.NormalPurchase
                           ? "Normal Satın Alma"
                           : s.RequestType == PurchaseRequestType.ResearchAndOffer
                               ? "Araştırma ve Teklif"
                               : "Bilinmiyor")

                  .Map(d => d.StatusName,
                       s => s.Status == PurchaseRequestStatus.Draft
                           ? "Taslak"
                           : s.Status == PurchaseRequestStatus.InProgress
                               ? "Devam Ediyor"
                               : s.Status == PurchaseRequestStatus.RevisionRequired
                                   ? "Revizyon Bekliyor"
                                   : s.Status == PurchaseRequestStatus.Completed
                                       ? "Tamamlandı"
                                       : s.Status == PurchaseRequestStatus.Rejected
                                           ? "Reddedildi"
                                           : s.Status == PurchaseRequestStatus.Cancelled
                                               ? "İptal Edildi"
                                               : "Bilinmiyor")

                  .Map(d => d.Items,
                       s => s.Items
                             .Where(x => !x.IsDeleted)
                             .OrderBy(x => x.LineNo))

                  .Map(d => d.Tasks,
                       s => s.Tasks
                             .Where(x => !x.IsDeleted)
                             .OrderByDescending(x => x.CreatedDate))

                  .Map(d => d.Histories,
                       s => s.Histories
                             .Where(x => !x.IsDeleted)
                             .OrderByDescending(x => x.CreatedDate))

                  .Map(d => d.Attachments,
                       s => s.Attachments
                             .Where(x => !x.IsDeleted)
                             .OrderByDescending(x => x.CreatedDate))

                  .Ignore(d => d.AvailableActions);


            // =======================================================
            // PurchaseRequestItem
            // =======================================================

            config.NewConfig<PurchaseRequestItemCreateDto, PurchaseRequestItem>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequest)
                  .Ignore(d => d.Product)
                  .Ignore(d => d.AlternateProduct)
                  .Ignore(d => d.CurrencyType);


            config.NewConfig<PurchaseRequestItemUpdateDto, PurchaseRequestItem>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequest)
                  .Ignore(d => d.Product)
                  .Ignore(d => d.AlternateProduct)
                  .Ignore(d => d.CurrencyType);


            config.NewConfig<PurchaseRequestItem, PurchaseRequestItemGetDto>()
                  .Map(d => d.ProductCode,
                       s => s.Product != null
                           ? s.Product.ProductCode
                           : null)

                  .Map(d => d.ProductName,
                       s => !string.IsNullOrEmpty(s.ProductName)
                           ? s.ProductName
                           : s.Product != null
                               ? s.Product.Description
                               : null)

                  .Map(d => d.AlternateProductCode,
                       s => s.AlternateProduct != null
                           ? s.AlternateProduct.ProductCode
                           : null)

                  .Map(d => d.AlternateProductName,
                       s => !string.IsNullOrEmpty(s.AlternateProductName)
                           ? s.AlternateProductName
                           : s.AlternateProduct != null
                               ? s.AlternateProduct.Description
                               : null)

                  .Map(d => d.CurrencyCode,
                       s => s.CurrencyType != null
                           ? s.CurrencyType.Code
                           : null)

                  .Map(d => d.CurrencyName,
                       s => s.CurrencyType != null
                           ? s.CurrencyType.Name
                           : null);


            // =======================================================
            // PurchaseRequestStep
            // =======================================================

            config.NewConfig<PurchaseRequestStepCreateDto, PurchaseRequestStep>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Actions);


            config.NewConfig<PurchaseRequestStepUpdateDto, PurchaseRequestStep>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id)
                  .Ignore(d => d.Actions);


            config.NewConfig<PurchaseRequestStep, PurchaseRequestStepGetDto>();


            // =======================================================
            // PurchaseRequestAction
            // =======================================================

            config.NewConfig<PurchaseRequestActionCreateDto, PurchaseRequestAction>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequestStep)
                  .Ignore(d => d.TargetStep);


            config.NewConfig<PurchaseRequestActionUpdateDto, PurchaseRequestAction>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequestStep)
                  .Ignore(d => d.TargetStep);


            config.NewConfig<PurchaseRequestAction, PurchaseRequestActionGetDto>()
                  .Map(d => d.PurchaseRequestStepCode,
                       s => s.PurchaseRequestStep != null
                           ? s.PurchaseRequestStep.Code
                           : null)

                  .Map(d => d.PurchaseRequestStepName,
                       s => s.PurchaseRequestStep != null
                           ? s.PurchaseRequestStep.Name
                           : null)

                  .Map(d => d.TargetStepCode,
                       s => s.TargetStep != null
                           ? s.TargetStep.Code
                           : null)

                  .Map(d => d.TargetStepName,
                       s => s.TargetStep != null
                           ? s.TargetStep.Name
                           : null);


            // =======================================================
            // PurchaseRequestTask
            // =======================================================

            config.NewConfig<PurchaseRequestTaskCreateDto, PurchaseRequestTask>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequest)
                  .Ignore(d => d.PurchaseRequestStep)
                  .Ignore(d => d.AssignedUser)
                  .Ignore(d => d.AssignedRole)
                  .Ignore(d => d.CompletedUser)
                  .Ignore(d => d.CompletedDate)
                  .Ignore(d => d.CompletedUserId);


            config.NewConfig<PurchaseRequestTask, PurchaseRequestTaskGetDto>()
                  .Map(d => d.StepCode,
                       s => s.PurchaseRequestStep != null
                           ? s.PurchaseRequestStep.Code
                           : null)

                  .Map(d => d.StepName,
                       s => s.PurchaseRequestStep != null
                           ? s.PurchaseRequestStep.Name
                           : null)

                  .Map(d => d.AssignedUserName,
                       s => s.AssignedUser != null
                           ? s.AssignedUser.TechnicianName
                           : null)

                  .Map(d => d.AssignedRoleName,
                       s => s.AssignedRole != null
                           ? s.AssignedRole.Name
                           : null)

                  .Map(d => d.CompletedUserName,
                       s => s.CompletedUser != null
                           ? s.CompletedUser.TechnicianName
                           : null)

                  .Map(d => d.StatusName,
                       s => s.Status == PurchaseRequestTaskStatus.Pending
                           ? "Bekliyor"
                           : s.Status == PurchaseRequestTaskStatus.Completed
                               ? "Tamamlandı"
                               : s.Status == PurchaseRequestTaskStatus.Cancelled
                                   ? "İptal Edildi"
                                   : "Bilinmiyor");


            // =======================================================
            // PurchaseRequestHistory
            // =======================================================

            config.NewConfig<PurchaseRequestHistoryCreateDto, PurchaseRequestHistory>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequest)
                  .Ignore(d => d.FromStep)
                  .Ignore(d => d.ToStep)
                  .Ignore(d => d.PurchaseRequestAction);


            config.NewConfig<PurchaseRequestHistory, PurchaseRequestHistoryGetDto>()
                  .Map(d => d.FromStepCode,
                       s => s.FromStep != null
                           ? s.FromStep.Code
                           : null)

                  .Map(d => d.FromStepName,
                       s => s.FromStep != null
                           ? s.FromStep.Name
                           : null)

                  .Map(d => d.ToStepCode,
                       s => s.ToStep != null
                           ? s.ToStep.Code
                           : null)

                  .Map(d => d.ToStepName,
                       s => s.ToStep != null
                           ? s.ToStep.Name
                           : null)

                  .Map(d => d.ActionCode,
                       s => s.PurchaseRequestAction != null
                           ? s.PurchaseRequestAction.Code
                           : null)

                  .Map(d => d.ActionName,
                       s => s.PurchaseRequestAction != null
                           ? s.PurchaseRequestAction.Name
                           : null)

                  // CreatedUser için navigation olmadığı için servis katmanında doldurulacak.
                  .Map(d => d.CreatedUserName, s => (string?)null);


            // =======================================================
            // PurchaseAttachment
            // =======================================================

            config.NewConfig<PurchaseAttachmentCreateDto, PurchaseAttachment>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.PurchaseRequest)
                  .Ignore(d => d.UploadedStep);


            config.NewConfig<PurchaseAttachment, PurchaseAttachmentGetDto>()
                  .Map(d => d.AttachmentTypeName,
                       s => s.AttachmentType == PurchaseAttachmentType.General
                           ? "Dosyalar"
                           : s.AttachmentType == PurchaseAttachmentType.Purchase
                               ? "Satınalma Dosyaları"
                               : "Bilinmiyor")

                  .Map(d => d.UploadedStepCode,
                       s => s.UploadedStep != null
                           ? s.UploadedStep.Code
                           : null)

                  .Map(d => d.UploadedStepName,
                       s => s.UploadedStep != null
                           ? s.UploadedStep.Name
                           : null)

                  // CreatedUser navigation olmadığı için servis katmanında doldurulacak.
                  .Map(d => d.CreatedUserName, s => (string?)null);

            #endregion CRM
        }

        // Helper metodlar için (MapsterConfig sınıfı içine ekleyin)
        static string GetFeedbackTypeText(FeedbackType type) => type switch
        {
            FeedbackType.Suggestion => "Öneri",
            FeedbackType.FeatureRequest => "Özellik Talebi",
            FeedbackType.BugReport => "Hata Bildirimi",
            FeedbackType.Issue => "Sorun",
            FeedbackType.Improvement => "İyileştirme",
            FeedbackType.Other => "Diğer",
            _ => "Bilinmiyor"
        };

        static string GetStatusText(FeedbackStatus status) => status switch
        {
            FeedbackStatus.Created => "Oluşturuldu",
            FeedbackStatus.UnderReview => "İnceleniyor",
            FeedbackStatus.InProgress => "Devam Ediyor",
            FeedbackStatus.Completed => "Tamamlandı",
            FeedbackStatus.Rejected => "Reddedildi",
            FeedbackStatus.Closed => "Kapatıldı",
            _ => "Bilinmiyor"
        };

        static List<string>? DeserializeAttachmentUrls(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            }
            catch
            {
                return null;
            }
        }

        static string GetPolicyTypeText(WorkingHourPolicyType type) => type switch
        {
            WorkingHourPolicyType.WeekdayDefault => "Hafta İçi Default",
            WorkingHourPolicyType.WeekendDefault => "Hafta Sonu Default",
            WorkingHourPolicyType.WeekDay => "Hafta Günü",
            WorkingHourPolicyType.PublicHoliday => "Resmi Tatil",
            WorkingHourPolicyType.SpecificDate => "Belirli Tarih",
            WorkingHourPolicyType.CustomDay => "Özel Gün",
            _ => "Bilinmiyor"
        };

        static string GetDayOfWeekText(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "Pazartesi",
            DayOfWeek.Tuesday => "Salı",
            DayOfWeek.Wednesday => "Çarşamba",
            DayOfWeek.Thursday => "Perşembe",
            DayOfWeek.Friday => "Cuma",
            DayOfWeek.Saturday => "Cumartesi",
            DayOfWeek.Sunday => "Pazar",
            _ => "Bilinmiyor"
        };
    }
}
