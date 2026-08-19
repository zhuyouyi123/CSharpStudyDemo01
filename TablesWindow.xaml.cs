using System.Windows;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class TablesWindow : Window
    {
        private SqlConnection? _connection;

        public TablesWindow(SqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
            LoadTables();
        }

        private void LoadTables()
        {
            if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;

            try
            {
                var tables = new System.Collections.Generic.List<TableInfo>();
                using var cmd = new SqlCommand(@"
                    SELECT 
                        ROW_NUMBER() OVER (ORDER BY t.name) AS [RowNum],
                        t.name AS [TableName],
                        p.rows AS [RowCount]
                    FROM sys.tables t
                    INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
                    WHERE t.is_ms_shipped = 0
                    ORDER BY t.name", _connection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tables.Add(new TableInfo
                    {
                        RowNum = reader.GetInt64(0).ToString(),
                        TableName = reader.GetString(1),
                        RowCount = reader.GetInt64(2).ToString("N0")
                    });
                }

                dgTables.ItemsSource = tables;
                txtCount.Text = "共 " + tables.Count + " 张表";
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载表结构失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class TableInfo
    {
        public string RowNum { get; set; } = "";
        public string TableName { get; set; } = "";
        public string RowCount { get; set; } = "";
    }
}