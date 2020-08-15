using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NetDaemon.Common;
using NetDaemon.Daemon.Config;

[assembly: InternalsVisibleTo("NetDaemon.Daemon.Tests")]

namespace NetDaemon.Daemon
{
    public sealed class CodeManager : IInstanceDaemonApp
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly YamlConfig _yamlConfig;
        private IEnumerable<INetDaemonAppBase>? _loadedDaemonApps;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="daemonAppTypes">App compiled app types</param>
        /// <param name="logger">ILogger instance to use</param>
        /// <param name="serviceProvider"></param>
        public CodeManager(ILogger logger, IServiceProvider serviceProvider, YamlConfig yamlConfig)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _yamlConfig = yamlConfig;
        }

        public int Count => _loadedDaemonApps.Count();

        public IEnumerable<INetDaemonAppBase> InstanceDaemonApps()
        {
            var result = new List<INetDaemonAppBase>(50);
            
            // Get all yaml config file paths
            var allConfigFilePaths = _yamlConfig.GetAllConfigFilePaths();

            if (!allConfigFilePaths.Any())
            {
                _logger.LogWarning("No yaml configuration files found, please add files to [netdaemonfolder]/apps");
                return result;
            }

            foreach (string file in allConfigFilePaths)
            {
                // TODO: Rework.  Use IOC

                var yamlAppConfig = new YamlAppConfig(new List<Type>(), File.OpenText(file), _yamlConfig, file, _serviceProvider);

                foreach (var appInstance in yamlAppConfig.Instances)
                {
                    result.Add(appInstance);
                }
            }
            return result;
        }
    }
}