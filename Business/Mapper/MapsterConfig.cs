using Mapster;
using Model.Concrete;
using Model.Dtos.Brand;
using Model.Dtos.CurrencyType;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.CustomerType;
using Model.Dtos.Model;
using Model.Dtos.Product;
using Model.Dtos.ProductType;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Role;
using Model.Dtos.ServiceType;
using Model.Dtos.SystemType;
using Model.Dtos.User;
using Model.Dtos.UserRole;

namespace Business.Mapper
{
    public  class MapsterConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
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


            // Küçük özet
            config.NewConfig<Model.Concrete.Model, ModelGetDto>();

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
                  .Ignore(d => d.Customer);

            config.NewConfig<ProgressApproverUpdateDto, ProgressApprover>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.Customer);

            config.NewConfig<ProgressApprover, ProgressApproverGetDto>();
          

            // ---------------- Role ----------------
            config.NewConfig<RoleCreateDto, Role>()
                  .Ignore(d => d.Id)
                  .Ignore(d => d.UserRoles);

            config.NewConfig<RoleUpdateDto, Role>()
                  .IgnoreNullValues(true)
                  .Ignore(d => d.UserRoles);

            config.NewConfig<Role, RoleGetDto>();

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
        }
    }
}
