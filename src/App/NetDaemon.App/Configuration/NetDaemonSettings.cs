﻿namespace NetDaemon.Configuration
{
    public class NetDaemonSettings
    {
        public bool? GenerateEntities { get; set; } = false;
        public string? SourceFolder { get; set; } = null;
        public string? ProjectFolder { get; set; } = string.Empty;
    }
}