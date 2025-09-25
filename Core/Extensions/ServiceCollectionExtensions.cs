using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAdvancedDependencyInjection(this IServiceCollection services)
        {
            return services.AddCommonServices();
        }

        private static IServiceCollection AddCommonServices(this IServiceCollection services)
        {
            services.TryAddSingleton(services);
            return services;
        }
        public static IServiceCollection AddDependencyResolvers(this IServiceCollection services,
            ICoreModule[] modules)
        {
            foreach (var module in modules)
            {
                module.Load(services);
            }

            return ServiceTool.Create(services);
        }
    }
}
