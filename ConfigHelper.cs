using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StudyDemo01
{
    public class DatabaseConfig
    {
        public string Server { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1433;
        public string Database { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Password { get; set; } = "";
        public bool TrustServerCertificate { get; set; } = true;
        public int ConnectTimeout { get; set; } = 15;

        public string GetConnectionString()
        {
            return "Data Source=" + Server + "," + Port + ";Initial Catalog=" + Database +
                   ";Persist Security Info=True;User ID=" + UserId + ";Password=" + Password +
                   ";Trust Server Certificate=" + TrustServerCertificate +
                   ";Connect Timeout=" + ConnectTimeout;
        }

        public string GetConnectionString(string database)
        {
            return "Data Source=" + Server + "," + Port + ";Initial Catalog=" + database +
                   ";Persist Security Info=True;User ID=" + UserId + ";Password=" + Password +
                   ";Trust Server Certificate=" + TrustServerCertificate +
                   ";Connect Timeout=" + ConnectTimeout;
        }
    }

    public class AgvConfig
    {
        public int MapRefreshInterval { get; set; } = 10;
        public string AgvOnLineColor { get; set; } = "#3B82F6";
        public string AgvOffLineColor { get; set; } = "#94A3B8";
        public string AgvAlarmColor { get; set; } = "#EF4444";
        public bool ShowUnlinkedStations { get; set; } = true;
        public bool ShowStationTitles { get; set; } = true;
    }

    public static class ConfigHelper
    {
        private static IConfiguration? _configuration;
        private static DatabaseConfig? _databaseConfig;
        private static AgvConfig? _agvConfig;
        private static readonly object _lock = new();

        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    lock (_lock)
                    {
                        if (_configuration == null)
                        {
                            var builder = new ConfigurationBuilder()
                                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                            _configuration = builder.Build();
                        }
                    }
                }
                return _configuration;
            }
        }

        public static DatabaseConfig DatabaseSettings
        {
            get
            {
                if (_databaseConfig == null)
                {
                    lock (_lock)
                    {
                        if (_databaseConfig == null)
                        {
                            _databaseConfig = new DatabaseConfig();
                            Configuration.GetSection("DatabaseSettings").Bind(_databaseConfig);
                        }
                    }
                }
                return _databaseConfig;
            }
        }

        public static AgvConfig AgvSettings
        {
            get
            {
                if (_agvConfig == null)
                {
                    lock (_lock)
                    {
                        if (_agvConfig == null)
                        {
                            _agvConfig = new AgvConfig();
                            Configuration.GetSection("AgvSettings").Bind(_agvConfig);
                        }
                    }
                }
                return _agvConfig;
            }
        }

        public static void Reload()
        {
            lock (_lock)
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                _configuration = builder.Build();
                _databaseConfig = null;
                _agvConfig = null;
            }
        }

        private static void WriteJsonConfig(object dbSettings, object agvSettings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var configObj = new { DatabaseSettings = dbSettings, AgvSettings = agvSettings };
            var json = JsonSerializer.Serialize(configObj, options);
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            File.WriteAllText(path, json);
            Reload();
        }

        public static void Save(DatabaseConfig config)
        {
            var agv = AgvSettings;
            WriteJsonConfig(config, agv);
        }

        public static void SaveAgvConfig(AgvConfig agvConfig)
        {
            var db = DatabaseSettings;
            WriteJsonConfig(db, agvConfig);
        }
    }
}
