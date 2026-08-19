using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class DatabaseSelectorDialog : Window
    {
        private readonly SqlConnection _connection;
        public string SelectedDatabase { get; private set; } = "";
        public int DatabaseCount { get; private set; } = 0;

        public DatabaseSelectorDialog(SqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
            Loaded += Window_Loaded;
            lstDatabases.MouseDoubleClick += (s, e) =>
            {
                if (lstDatabases.SelectedItem != null) BtnOk_Click(s, e);
            };
        }

        public DatabaseSelectorDialog(List<string> databases)
        {
            InitializeComponent();
            _connection = null!;
            DatabaseCount = databases.Count;
            Loaded += (s, e) =>
            {
                lstDatabases.ItemsSource = databases;
                txtLoading.Visibility = Visibility.Collapsed;
                btnOk.IsEnabled = true;
                if (databases.Count == 1)
                {
                    SelectedDatabase = databases[0];
                    DialogResult = true;
                }
            };
            lstDatabases.MouseDoubleClick += (s, e) =>
            {
                if (lstDatabases.SelectedItem != null) BtnOk_Click(s, e);
            };
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT name FROM sys.databases 
                    WHERE name NOT IN ('master','tempdb','model','msdb')
                    AND state_desc = 'ONLINE'
                    ORDER BY name", _connection);

                using var reader = await cmd.ExecuteReaderAsync();
                var databases = new List<string>();
                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }

                DatabaseCount = databases.Count;
                lstDatabases.ItemsSource = databases;
                txtLoading.Visibility = Visibility.Collapsed;
                btnOk.IsEnabled = true;

                if (databases.Count == 1)
                {
                    SelectedDatabase = databases[0];
                    DialogResult = true;
                }
                else if (databases.Count == 0)
                {
                    txtLoading.Text = "未找到可用数据库";
                }
            }
            catch (Exception ex)
            {
                txtLoading.Text = "加载失败: " + ex.Message;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (lstDatabases.SelectedItem is string db)
            {
                SelectedDatabase = db;
                DialogResult = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}