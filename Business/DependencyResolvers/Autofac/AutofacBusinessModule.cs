using Autofac;
using Autofac.Extras.DynamicProxy;
using Business.Interfaces;
using Business.Services;
using Business.Utilities.Security;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Core.Utilities.IoC;
using Core.Utilities.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Business.DependencyResolvers.Autofac
{
    public class AutofacBusinessModule: Module, ICoreModule
    {
        public void Load(IServiceCollection services)
        {
            services.AddScoped(typeof(IUserService), typeof(UserService));
            services.AddScoped(typeof(IPasswordHasherService), typeof(IdentityPasswordHasherService));
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
