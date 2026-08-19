using System.Windows;

namespace StudyDemo01
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Function1_Click(object sender, RoutedEventArgs e)
        {
            var dbWindow = new DbManager();
            dbWindow.Owner = this;
            this.Hide();
            dbWindow.Show();
        }

        private async void Function2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigHelper.DatabaseSettings;
                var connectionString = config.GetConnectionString("master");

                using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                await connection.OpenAsync();

                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT name FROM sys.databases 
                    WHERE name LIKE 'Szgh_Agv_%' AND state_desc = 'ONLINE'
                    ORDER BY name", connection);

                using var reader = await cmd.ExecuteReaderAsync();
                var databases = new List<string>();
                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }

                if (databases.Count == 0)
                {
                    MessageBox.Show("未找到以 Szgh_Agv_ 开头的数据库", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedDb;
                if (databases.Count == 1)
                {
                    selectedDb = databases[0];
                }
                else
                {
                    var selector = new DatabaseSelectorDialog(databases);
                    selector.Owner = this;
                    if (selector.ShowDialog() != true) return;
                    selectedDb = selector.SelectedDatabase;
                }

                var agvWindow = new AgvManager(selectedDb);
                agvWindow.Owner = this;
                this.Hide();
                agvWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("连接数据库失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}