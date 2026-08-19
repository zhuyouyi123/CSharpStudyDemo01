using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class DbManager : Window
    {
        private bool _isConnected = false;
        private SqlConnection? _currentConnection;
        private string _currentDatabase = "";

        public DbManager()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected) return;

            btnConnect.IsEnabled = false;
            btnConnect.Content = "连接中...";
            HideMessages();

            try
            {
                var config = ConfigHelper.DatabaseSettings;
                var connectionString = config.GetConnectionString("master");

                _currentConnection = new SqlConnection(connectionString);
                await _currentConnection.OpenAsync();

                var selector = new DatabaseSelectorDialog(_currentConnection);
                selector.Owner = this;
                string selectedDb;

                // 先查询数据库列表，再决定是否弹窗
                var dbList = new List<string>();
                using (var cmd = new SqlCommand(@"
                    SELECT name FROM sys.databases 
                    WHERE name NOT IN ('master','tempdb','model','msdb')
                    AND state_desc = 'ONLINE'
                    ORDER BY name", _currentConnection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        dbList.Add(reader.GetString(0));
                }

                if (dbList.Count == 0)
                {
                    _currentConnection.Close();
                    _currentConnection.Dispose();
                    _currentConnection = null;
                    btnConnect.IsEnabled = true;
                    btnConnect.Content = "连接数据库";
                    ShowError("错误", "未找到可用数据库");
                    return;
                }
                else if (dbList.Count == 1)
                {
                    selectedDb = dbList[0];
                }
                else
                {
                    var listSelector = new DatabaseSelectorDialog(dbList);
                    listSelector.Owner = this;
                    if (listSelector.ShowDialog() != true)
                    {
                        _currentConnection.Close();
                        _currentConnection.Dispose();
                        _currentConnection = null;
                        btnConnect.IsEnabled = true;
                        btnConnect.Content = "连接数据库";
                        return;
                    }
                    selectedDb = listSelector.SelectedDatabase;
                }
                await _currentConnection.ChangeDatabaseAsync(selectedDb);
                _currentDatabase = selectedDb;

                _isConnected = true;
                UpdateStatus(true);
                ShowSuccess("成功连接到数据库 [" + selectedDb + "]");

                btnDisconnect.IsEnabled = true;
                btnShowTables.IsEnabled = true;
                btnExecSql.IsEnabled = true;
                btnBackup.IsEnabled = true;
                txtHint.Text = "数据库已连接，可使用上方功能按钮";
                txtHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            catch (SqlException ex)
            {
                _isConnected = false;
                UpdateStatus(false);
                ShowSqlError(ex);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                UpdateStatus(false);
                ShowError("连接失败", ex.Message);
            }
            finally
            {
                btnConnect.IsEnabled = !_isConnected;
                btnConnect.Content = "连接数据库";
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected) return;

            try
            {
                if (_currentConnection != null)
                {
                    _currentConnection.Close();
                    _currentConnection.Dispose();
                    _currentConnection = null;
                }
                _isConnected = false;
                UpdateStatus(false);
                ShowSuccess("已成功断开数据库连接");

                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
                btnShowTables.IsEnabled = false;
                btnExecSql.IsEnabled = false;
                btnBackup.IsEnabled = false;
                txtHint.Text = "请先连接数据库后再使用数据操作功能";
                txtHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            }
            catch (Exception ex)
            {
                ShowError("断开连接失败", ex.Message);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ConfigHelper.Reload();
            HideMessages();
            ShowSuccess("配置已刷新");
        }

        private void BtnChangeConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EditConfigDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                HideMessages();
                ShowSuccess("配置已更改，点击「连接数据库」使用新配置");
            }
        }

        private void BtnShowTables_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConnection == null || _currentConnection.State != System.Data.ConnectionState.Open) return;
            var win = new TablesWindow(_currentConnection);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnExecSql_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConnection == null || _currentConnection.State != System.Data.ConnectionState.Open) return;
            var win = new SqlExecWindow(_currentConnection, _currentDatabase);
            win.Owner = this;
            win.ShowDialog();
        }

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConnection == null || _currentConnection.State != System.Data.ConnectionState.Open) return;

            var dialog = new SaveBackupDialog(_currentDatabase);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            var serverPath = dialog.SelectedPath;
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = _currentDatabase + "_" + timestamp + ".bak";
            var fullPath = serverPath.TrimEnd('\\') + "\\" + fileName;

            loadingOverlay.Visibility = Visibility.Visible;
            txtLoadingStatus.Text = "正在备份数据库...";
            txtLoadingDetail.Text = _currentDatabase + " -> " + fullPath;

            try
            {
                txtLoadingStatus.Text = "正在备份数据库...";
                txtLoadingDetail.Text = _currentDatabase + " -> " + fullPath;

                using var cmd = new SqlCommand(@"
                    BACKUP DATABASE [" + _currentDatabase + @"] 
                    TO DISK = N'" + fullPath.Replace("'", "''") + @"' 
                    WITH FORMAT, COMPRESSION, STATS = 10", _currentConnection);
                cmd.CommandTimeout = 600;

                await cmd.ExecuteNonQueryAsync();

                txtLoadingStatus.Text = "备份完成!";
                txtLoadingDetail.Text = "文件: " + fullPath;
                progressBackup.IsIndeterminate = false;
                progressBackup.Value = 100;

                await Task.Delay(2000);

                loadingOverlay.Visibility = Visibility.Collapsed;
                progressBackup.IsIndeterminate = true;
                progressBackup.Value = 0;

                ShowSuccess("数据库备份成功!\n文件位置: " + fullPath);
            }
            catch (Exception ex)
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                progressBackup.IsIndeterminate = true;
                progressBackup.Value = 0;

                var msg = ex.Message;
                if (msg.Contains("系统找不到指定的路径") || msg.Contains("cannot find the path"))
                {
                    var config = ConfigHelper.DatabaseSettings;
                    var serverIp = config.Server.Split(',')[0].Trim();
                    msg = "备份失败，请检查服务器 [" + serverIp + "] 上目录是否存在: " + serverPath;
                }
                ShowError("备份失败", msg);
            }
        }

        private void BtnRestoreGuide_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RestoreGuideDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void UpdateStatus(bool connected)
        {
            var color = connected ? "#10B981" : "#EF4444";
            var text = connected ? "已连接" : "未连接";
            StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            StatusText.Text = text;
        }

        private void HideMessages()
        {
            SuccessBorder.Visibility = Visibility.Collapsed;
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void ShowSuccess(string message)
        {
            SuccessBorder.Visibility = Visibility.Visible;
            ErrorBorder.Visibility = Visibility.Collapsed;
            SuccessText.Text = message;
        }

        private void ShowError(string title, string detail)
        {
            ErrorBorder.Visibility = Visibility.Visible;
            SuccessBorder.Visibility = Visibility.Collapsed;
            ErrorTitle.Text = title;
            ErrorDetail.Text = detail;
        }

        private void ShowSqlError(SqlException ex)
        {
            ErrorBorder.Visibility = Visibility.Visible;
            SuccessBorder.Visibility = Visibility.Collapsed;
            ErrorTitle.Text = "数据库连接失败 (错误号: " + ex.Number + ")";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("异常类型: " + ex.GetType().Name);
            sb.AppendLine("错误号: " + ex.Number);
            sb.AppendLine("错误消息: " + ex.Message);
            sb.AppendLine("服务器: " + ex.Server);
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("内部异常: " + ex.InnerException.GetType().Name);
                sb.AppendLine("内部消息: " + ex.InnerException.Message);
            }
            ErrorDetail.Text = sb.ToString().TrimEnd();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                var result = MessageBox.Show("数据库当前处于连接状态，确定要返回吗？",
                    "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) return;
                try
                {
                    if (_currentConnection != null)
                    {
                        _currentConnection.Close();
                        _currentConnection.Dispose();
                        _currentConnection = null;
                    }
                    _isConnected = false;
                }
                catch { }
            }
            this.Close();
            if (Owner != null) Owner.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (_currentConnection != null)
                {
                    _currentConnection.Close();
                    _currentConnection.Dispose();
                    _currentConnection = null;
                }
                _isConnected = false;
            }
            catch { }
            base.OnClosed(e);
        }
    }
}