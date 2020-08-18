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
        private readonly YamlConfig _yamlConfig;
        private readonly IAppTypeFactory _appTypeFactory;
        private int _numberOfAppsFound;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="daemonAppTypes">App compiled app types</param>
        /// <param name="logger">ILogger instance to use</param>
        /// <param name="serviceProvider"></param>
        /// <param name="appTypeFactory"></param>
        public CodeManager(ILogger logger, YamlConfig yamlConfig, IAppTypeFactory appTypeFactory)
        {
            _logger = logger;
            _yamlConfig = yamlConfig;
            _appTypeFactory = appTypeFactory;
            _numberOfAppsFound = 0;
        }

        public int Count => _numberOfAppsFound;

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
                var yamlAppConfig = new YamlAppConfig(File.OpenText(file), _yamlConfig, file, _appTypeFactory);

                foreach (var appInstance in yamlAppConfig.Instances)
                {
                    result.Add(appInstance);
                    _numberOfAppsFound++;
                }
            }
            return result;
        }
    }
}