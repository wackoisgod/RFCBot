using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Nett;

namespace RFCBot
{
    public class Config
    {
        public string? databaseConnectionString { get; set; }
        public string? githubURL { get; set; }
        public string githubAccessToken { get; set; }
        public string githubUserAgent { get; set; } = "RFCBot";
        public List<string> githubWebHookSecrets { get; set; } = new List<string>();
        public long githubIntervalMins { get; set; }

        public static Lazy<Config> CONFIG = new Lazy<Config>(() => Init());

        public static Config Init()
        {
            if (!File.Exists("config.toml")) {
                return new Config();
            }

            return Toml.ReadFile<Config>("config.toml");
        }
    }
}
