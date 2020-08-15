﻿using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetDaemon;
using Serilog;

namespace Service
{
    internal class Program
    {
        private const string HassioConfigPath = "/data/options.json";

        public static async Task Main(string[] args)
        {
            try
            {
                Log.Logger = SerilogConfigurator.Configure().CreateLogger();
                var appStartLogging = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger, dispose: true)).CreateLogger<Program>();

                if (File.Exists(HassioConfigPath))
                    await ReadHassioConfig();

                await Host.CreateDefaultBuilder(args)
                    .ConfigureLogging(builder => builder.AddSerilog(Log.Logger))
                    .UseNetDaemon(appStartLogging)
                    .Build()
                    .RunAsync();
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Failed to start host...");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public void ValidateSourceFolder(string sourceFolder)
        {
            // Automatically create source directories
            if (!Directory.Exists(sourceFolder))
                Directory.CreateDirectory(sourceFolder);

        }

        private static async Task ReadHassioConfig()
        {
            try
            {
                var hassAddOnSettings = await JsonSerializer.DeserializeAsync<HassioConfig>(File.OpenRead(HassioConfigPath)).ConfigureAwait(false);

                if (hassAddOnSettings.LogLevel is object)
                    SerilogConfigurator.SetMinimumLogLevel(hassAddOnSettings.LogLevel);

                if (hassAddOnSettings.GenerateEntitiesOnStart is object)
                    Environment.SetEnvironmentVariable("NETDAEMON__GENERATEENTITIES", hassAddOnSettings.GenerateEntitiesOnStart.ToString());

                if (hassAddOnSettings.LogMessages is object && hassAddOnSettings.LogMessages == true)
                    Environment.SetEnvironmentVariable("HASSCLIENT_MSGLOGLEVEL", "Default");

                if (hassAddOnSettings.ProjectFolder is object && string.IsNullOrEmpty(hassAddOnSettings.ProjectFolder) == false)
                    Environment.SetEnvironmentVariable("NETDAEMON__PROJECTFOLDER", hassAddOnSettings.ProjectFolder);

                // We are in Hassio so hard code the path
                Environment.SetEnvironmentVariable("NETDAEMON__APPFOLDER", "/config/netdaemon");
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Failed to read the Home Assistant Add-on config");
            }
        }
    }
}