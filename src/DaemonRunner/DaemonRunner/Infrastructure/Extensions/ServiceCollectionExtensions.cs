using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace NetDaemon.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Stolen from https://medium.com/agilix/asp-net-core-inject-all-instances-of-a-service-interface-64b37b43fdc8
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="assembly"></param>
        /// <param name="lifetime"></param>
        public static void RegisterAllTypes<T>(this IServiceCollection services, Assembly assembly, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            var typesFromAssemblies = assembly.GetTypesWhereBaseType<T>();

            foreach (var type in typesFromAssemblies)
            {
                services.Add(new ServiceDescriptor(type, type, lifetime));
            }
        }
    }

    public static class AssemblyExtensions
    {
        public static IEnumerable<Type> GetTypesWhereBaseType<T>(this Assembly assembly)
        {
            return assembly.ExportedTypes.Where(x => x.BaseType == typeof(T));
        }
    }
}