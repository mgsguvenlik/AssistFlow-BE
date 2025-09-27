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
            services.AddScoped(typeof(IPasswordHasherService), typeof(IdentityPasswordHasherService));

            services.AddScoped(typeof(IUserService), typeof(UserService));
            services.AddScoped(typeof(IRoleService), typeof(RoleService));

            // ASP.NET Core Identity hasher kaydı
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

            // Eğer kendi sarmalayıcını (IPasswordHasherService) da kullanacaksan:
            services.AddScoped<IPasswordHasherService, IdentityPasswordHasherService>();

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
