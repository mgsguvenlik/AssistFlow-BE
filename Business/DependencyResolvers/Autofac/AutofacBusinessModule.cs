using Autofac;
using Autofac.Extras.DynamicProxy;
using Business.Interfaces;
using Business.Services;
using Business.Utilities.Security;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Core.Utilities.IoC;
using Core.Utilities.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Model.Concrete;

namespace Business.DependencyResolvers.Autofac
{
    public class AutofacBusinessModule: Module, ICoreModule
    {
        public void Load(IServiceCollection services)
        {
            services.AddScoped(typeof(IAuthService), typeof(AuthService));
            services.AddScoped(typeof(IBrandService), typeof(BrandService));
            services.AddScoped(typeof(ICityService), typeof(CityService));
            services.AddScoped(typeof(ICurrencyTypeService), typeof(CurrencyTypeService));
            services.AddScoped(typeof(ICustomerGroupService), typeof(CustomerGroupService));
            services.AddScoped(typeof(ICustomerService), typeof(CustomerService));
            services.AddScoped(typeof(ICustomerTypeService), typeof(CustomerTypeService));
            services.AddScoped(typeof(IModelService), typeof(ModelService));
            services.AddScoped(typeof(IProductService), typeof(ProductService));
            services.AddScoped(typeof(IProductTypeService), typeof(ProductTypeService));
            services.AddScoped(typeof(IProgressApproverService), typeof(ProgressApproverService));
            services.AddScoped(typeof(IRoleService), typeof(RoleService));
            services.AddScoped(typeof(IServiceTypeService), typeof(ServiceTypeService));
            services.AddScoped(typeof(ISystemTypeService), typeof(SystemTypeService));
            services.AddScoped(typeof(IUserService), typeof(UserService));
            services.AddScoped(typeof(IConfigurationService), typeof(ConfigurationService));
            services.AddScoped(typeof(IMailService), typeof(MailService));

            // ASP.NET Core Identity hasher kaydı
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

            // Eğer kendi sarmalayıcını (IPasswordHasherService) da kullanacaksan:
            services.AddScoped(typeof(IPasswordHasherService), typeof(IdentityPasswordHasherService));




            // ---- CRUD generic kayıtlar ----

            // Brand
            services.AddScoped<
                ICrudService<Model.Dtos.Brand.BrandCreateDto,
                             Model.Dtos.Brand.BrandUpdateDto,
                             Model.Dtos.Brand.BrandGetDto,
                             long>, BrandService>();

            // CurrencyType
            services.AddScoped<
                ICrudService<Model.Dtos.CurrencyType.CurrencyTypeCreateDto,
                             Model.Dtos.CurrencyType.CurrencyTypeUpdateDto,
                             Model.Dtos.CurrencyType.CurrencyTypeGetDto,
                             long>, CurrencyTypeService>();

            // Customer
            services.AddScoped<
                ICrudService<Model.Dtos.Customer.CustomerCreateDto,
                             Model.Dtos.Customer.CustomerUpdateDto,
                             Model.Dtos.Customer.CustomerGetDto,
                             long>, CustomerService>();

            // CustomerGroup
            services.AddScoped<
                ICrudService<Model.Dtos.CustomerGroup.CustomerGroupCreateDto,
                             Model.Dtos.CustomerGroup.CustomerGroupUpdateDto,
                             Model.Dtos.CustomerGroup.CustomerGroupGetDto,
                             long>, CustomerGroupService>();

            // CustomerType
            services.AddScoped<
                ICrudService<Model.Dtos.CustomerType.CustomerTypeCreateDto,
                             Model.Dtos.CustomerType.CustomerTypeUpdateDto,
                             Model.Dtos.CustomerType.CustomerTypeGetDto,
                             long>, CustomerTypeService>();

            // Model
            services.AddScoped<
                ICrudService<Model.Dtos.Model.ModelCreateDto,
                             Model.Dtos.Model.ModelUpdateDto,
                             Model.Dtos.Model.ModelGetDto,
                             long>, ModelService>();

            // Product
            services.AddScoped<
                ICrudService<Model.Dtos.Product.ProductCreateDto,
                             Model.Dtos.Product.ProductUpdateDto,
                             Model.Dtos.Product.ProductGetDto,
                             long>, ProductService>();

            // ProductType
            services.AddScoped<
                ICrudService<Model.Dtos.ProductType.ProductTypeCreateDto,
                             Model.Dtos.ProductType.ProductTypeUpdateDto,
                             Model.Dtos.ProductType.ProductTypeGetDto,
                             long>, ProductTypeService> ();

            // ProgressApprover
            services.AddScoped<
                ICrudService<Model.Dtos.ProgressApprover.ProgressApproverCreateDto,
                             Model.Dtos.ProgressApprover.ProgressApproverUpdateDto,
                             Model.Dtos.ProgressApprover.ProgressApproverGetDto,
                             long>, ProgressApproverService>();

            // ServiceType
            services.AddScoped<
                ICrudService<Model.Dtos.ServiceType.ServiceTypeCreateDto,
                             Model.Dtos.ServiceType.ServiceTypeUpdateDto,
                             Model.Dtos.ServiceType.ServiceTypeGetDto,
                             long>, ServiceTypeService>();

            // SystemType
            services.AddScoped<
                ICrudService<Model.Dtos.SystemType.SystemTypeCreateDto,
                             Model.Dtos.SystemType.SystemTypeUpdateDto,
                             Model.Dtos.SystemType.SystemTypeGetDto,
                             long>, SystemTypeService>();

            // (Zaten var) Role ve User:
            services.AddScoped<
                ICrudService<Model.Dtos.Role.RoleCreateDto,
                             Model.Dtos.Role.RoleUpdateDto,
                             Model.Dtos.Role.RoleGetDto,
                             long>, RoleService>();

            services.AddScoped<
                ICrudService<Model.Dtos.User.UserCreateDto,
                             Model.Dtos.User.UserUpdateDto,
                             Model.Dtos.User.UserGetDto,
                             long>, UserService>();


            services.AddScoped<
             ICrudService<Model.Dtos.Role.RoleCreateDto,
                          Model.Dtos.Role.RoleUpdateDto,
                          Model.Dtos.Role.RoleGetDto,
                          long>, RoleService>();

            services.AddScoped<
                ICrudService<Model.Dtos.User.UserCreateDto,
                             Model.Dtos.User.UserUpdateDto,
                             Model.Dtos.User.UserGetDto,
                             long>, UserService>();

            services.AddScoped<
              ICrudService<Model.Dtos.Configuration.ConfigurationCreateDto,
                           Model.Dtos.Configuration.ConfigurationUpdateDto,
                           Model.Dtos.Configuration.ConfigurationGetDto,
                           long>, ConfigurationService>();
        }
        protected override void Load(ContainerBuilder builder)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces()
                .EnableInterfaceInterceptors(new ProxyGenerationOptions()
                { Selector = new AspectInterceptorSelector() })
                .SingleInstance();
        }
    }
}
