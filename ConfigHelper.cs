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

    public static class ConfigHelper
    {
        private static IConfiguration? _configuration;
        private static DatabaseConfig? _databaseConfig;

        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    _configuration = builder.Build();
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
                    _databaseConfig = new DatabaseConfig();
                    Configuration.GetSection("DatabaseSettings").Bind(_databaseConfig);
                }
                return _databaseConfig;
            }
        }

        public static void Reload()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            _configuration = builder.Build();
            _databaseConfig = null;
        }
    }
}