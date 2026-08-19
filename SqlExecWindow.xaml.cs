using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class SqlExecWindow : Window
    {
        private SqlConnection? _connection;
        private readonly string _savedSqlFolder;

        public SqlExecWindow(SqlConnection connection, string databaseName)
        {
            InitializeComponent();
            _connection = connection;
            _savedSqlFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedSQL", databaseName);
            Directory.CreateDirectory(_savedSqlFolder);

            txtSql.TextChanged += TxtSql_TextChanged;
            txtSql.KeyDown += TxtSql_KeyDown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTableList();
            LoadSavedSqlList();
        }

        private void LoadTableList()
        {
            if (_connection == null || _connection.State != ConnectionState.Open) return;
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT t.name FROM sys.tables t
                    WHERE t.is_ms_shipped = 0 ORDER BY t.name", _connection);
                using var reader = cmd.ExecuteReader();
                var tables = new List<string>();
                while (reader.Read()) tables.Add(reader.GetString(0));
                lstTables.ItemsSource = tables;
                txtTableCount.Text = "共 " + tables.Count + " 张表";
            }
            catch { txtTableCount.Text = "加载表列表失败"; }
        }

        private void LoadSavedSqlList()
        {
            var files = Directory.GetFiles(_savedSqlFolder, "*.sql");
            var items = files
                .OrderByDescending(f => File.GetCreationTime(f))
                .Select((f, i) => new SavedSqlItem
                {
                    RowNum = i + 1,
                    Name = Path.GetFileNameWithoutExtension(f),
                    FullPath = f
                })
                .ToList();
            lstSavedSql.ItemsSource = items;
        }

        private void RbTab_Checked(object sender, RoutedEventArgs e)
        {
            if (lstTables == null || panelSavedSql == null) return;
            if (rbTabTables.IsChecked == true)
            {
                lstTables.Visibility = Visibility.Visible;
                panelSavedSql.Visibility = Visibility.Collapsed;
            }
            else
            {
                lstTables.Visibility = Visibility.Collapsed;
                panelSavedSql.Visibility = Visibility.Visible;
                LoadSavedSqlList();
            }
        }

        private void TxtSql_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtPlaceholder.Visibility = string.IsNullOrEmpty(txtSql.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtSql_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                BtnExecute_Click(sender, e);
                e.Handled = true;
            }
        }

        private void LstTables_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstTables.SelectedItem is string tableName)
            {
                txtSql.Text = "SELECT TOP 100 * FROM [" + tableName + "]";
                txtSql.Focus();
            }
        }

        private void LstSavedSql_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSavedSql.SelectedItem is SavedSqlItem item)
            {
                try
                {
                    txtSql.Text = File.ReadAllText(item.FullPath);
                    txtSql.Focus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("读取失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LstSavedSql_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSavedSql.SelectedItem is SavedSqlItem item)
            {
                try
                {
                    txtSql.Text = File.ReadAllText(item.FullPath);
                    txtSql.Focus();
                }
                catch { }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var sql = txtSql.Text.Trim();
            if (string.IsNullOrEmpty(sql))
            {
                MessageBox.Show("请输入要保存的 SQL 语句", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new SaveSqlDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;
            var name = dialog.SqlName.Trim();
            var filePath = Path.Combine(_savedSqlFolder, name + ".sql");
            if (File.Exists(filePath))
            {
                var result = MessageBox.Show("名称 \"" + name + "\" 已存在，是否覆盖？",
                    "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }
            try
            {
                File.WriteAllText(filePath, sql);
                ShowSaveSuccess("保存成功: " + name + ".sql");
                LoadSavedSqlList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteSaved_Click(object sender, RoutedEventArgs e)
        {
            if (lstSavedSql.SelectedItem is not SavedSqlItem item) return;
            var result = MessageBox.Show("确定删除 \"" + item.Name + "\" 吗？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            try
            {
                File.Delete(item.FullPath);
                LoadSavedSqlList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSaveSuccess(string msg)
        {
            txtExecInfo.Text = msg;
            txtExecInfo.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            var sql = txtSql.Text.Trim();
            if (string.IsNullOrEmpty(sql))
            {
                MessageBox.Show("请输入 SQL 语句", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                MessageBox.Show("数据库连接已断开", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                var startTime = DateTime.Now;
                using var cmd = new SqlCommand(sql, _connection);
                cmd.CommandTimeout = 30;
                using var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                dgResult.ItemsSource = null;
                if (dt.Rows.Count > 0)
                {
                    dgResult.ItemsSource = dt.DefaultView;
                    dgResult.Visibility = Visibility.Visible;
                    txtMessage.Visibility = Visibility.Collapsed;
                    txtExecInfo.Text = "返回 " + dt.Rows.Count + " 行 | " + dt.Columns.Count + " 列 | " + elapsed.ToString("F0") + "ms";
                    txtExecInfo.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                }
                else
                {
                    dgResult.Visibility = Visibility.Collapsed;
                    txtMessage.Visibility = Visibility.Visible;
                    txtMessage.Text = "执行成功，无返回数据";
                    txtExecInfo.Text = "耗时 " + elapsed.ToString("F0") + "ms";
                    txtExecInfo.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B"));
                }
            }
            catch (SqlException ex)
            {
                dgResult.Visibility = Visibility.Collapsed;
                txtMessage.Visibility = Visibility.Visible;
                txtMessage.Text = "执行失败 (错误号: " + ex.Number + ")\n" + ex.Message;
                txtMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
                txtExecInfo.Text = "";
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtSql.Clear();
            dgResult.ItemsSource = null;
            dgResult.Visibility = Visibility.Collapsed;
            txtMessage.Visibility = Visibility.Visible;
            txtMessage.Text = "等待执行...";
            txtMessage.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B"));
            txtExecInfo.Text = "";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class SavedSqlItem
    {
        public int RowNum { get; set; }
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
    }
}