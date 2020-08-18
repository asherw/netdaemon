using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetDaemon.Common;
using NetDaemon.Common.Reactive;
using NetDaemon.Configuration;
using NetDaemon.Daemon;
using NetDaemon.Daemon.Config;
using NetDaemon.Infrastructure.Extensions;
using NetDaemon.Service;
using NetDaemon.Service.App;
using NetDaemon.Service.Configuration;

namespace NetDaemon
{
    public static class NetDaemonExtensions
    {
        public static IHostBuilder UseNetDaemon(this IHostBuilder hostBuilder, ILogger logger)
        {
            // TODO: clean up.
            var sourceFolder = @"E:/Programming/netdaemon/netdaemon-app-template/";

            return hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddSingleton(typeof(IAppTypeFactory), typeof(NetDaemonAppFactory));

                // TODO: clean this up.
                var appCodeFolder = Path.Combine(sourceFolder, "apps");
                var appsAssembly = DaemonCompiler.GetCompiledAppsAssembly(appCodeFolder, logger);

                services.RegisterAllTypes<NetDaemonRxApp>(appsAssembly, ServiceLifetime.Singleton);
                services.RegisterAllTypes<NetDaemonApp>(appsAssembly, ServiceLifetime.Singleton);

                var ndApps = new NetDaemonApps();

                var appTypes = appsAssembly.GetTypesWhereBaseType<NetDaemonRxApp>();
                AppNdApps(ndApps, appTypes);

                appTypes = appsAssembly.GetTypesWhereBaseType<NetDaemonApp>();
                AppNdApps(ndApps, appTypes);

                services.AddSingleton(ndApps);

                services.Configure<HomeAssistantSettings>(context.Configuration.GetSection("HomeAssistant"));
                services.Configure<NetDaemonSettings>(context.Configuration.GetSection("NetDaemon"));

                services.AddTransient<ILogger>(_ => logger);
                services.AddSingleton<IInstanceDaemonApp, CodeManager>();
                services.AddSingleton<YamlConfig>();

                services.AddHttpClient();
                services.AddHostedService<RunnerService>();
            });
        }

        private static void AppNdApps(NetDaemonApps ndApps, IEnumerable<Type> appTypes)
        {
            foreach (var appType in appTypes)
            {
                ndApps.RegisterApp(appType.FullName!, appType);
            }
        }
    }

    public class NetDaemonAppFactory : IAppTypeFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly NetDaemonApps _apps;

        public NetDaemonAppFactory(IServiceProvider serviceProvider, NetDaemonApps apps)
        {
            _serviceProvider = serviceProvider;
            _apps = apps;
        }

        public INetDaemonAppBase? ResolveByClassName(string className)
        {
            var appType = _apps.GetAppType(className);

            if (appType == null)
                return null;

            return _serviceProvider.GetService(appType) as INetDaemonAppBase;
        }
    }

    public class NetDaemonApps
    {
        private Dictionary<string, Type> Apps { get; }

        public NetDaemonApps()
        {
            Apps = new Dictionary<string, Type>();
        }

        public void RegisterApp(string appClassName, Type appType)
        {
            Apps[appClassName] = appType;
        }

        public Type? GetAppType(string appClassName)
        {
            if (Apps.ContainsKey(appClassName))
                return Apps[appClassName];

            return null;
        }
    }

}