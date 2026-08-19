using System.IO;
using System.Windows;

namespace StudyDemo01
{
    public partial class SaveBackupDialog : Window
    {
        public string SelectedPath { get; private set; } = "";
        private readonly string _dbName;
        private string _remotePath = "";

        public SaveBackupDialog(string dbName)
        {
            InitializeComponent();
            _dbName = dbName;

            LoadRemotePath();
            UpdateFileName();
        }

        private void LoadRemotePath()
        {
            try
            {
                var connStr = ConfigHelper.DatabaseSettings.GetConnectionString("master");
                foreach (var part in connStr.Split(';'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim().ToLower();
                        var val = kv[1].Trim();
                        if (key == "data source" || key == "server")
                        {
                            var server = val.Contains("\\") ? val.Split('\\')[0] : val;
                            _remotePath = $@"\\{server}\C$\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup";
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(_remotePath))
                _remotePath = @"\\192.168.190.100\C$\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup";

            txtRemotePath.Text = _remotePath;
            txtFullPath.Text = Path.Combine(_remotePath, txtFileName.Text + ".bak");
        }

        private void UpdateFileName()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            txtFileName.Text = _dbName + "_" + timestamp;
            txtFullPath.Text = Path.Combine(_remotePath, txtFileName.Text + ".bak");
        }

        private void TxtFileName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtFileName != null && txtFullPath != null)
            {
                txtFullPath.Text = Path.Combine(_remotePath, txtFileName.Text + ".bak");
            }
        }

        private void BtnChangePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择备份目录",
                InitialDirectory = _remotePath
            };

            if (dialog.ShowDialog() == true)
            {
                _remotePath = dialog.FolderName;
                txtRemotePath.Text = _remotePath;
                txtFullPath.Text = Path.Combine(_remotePath, txtFileName.Text + ".bak");
            }
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFileName.Text))
            {
                MessageBox.Show("请输入文件名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var fileName = txtFileName.Text.Trim();
            if (!fileName.EndsWith(".bak"))
                fileName += ".bak";

            SelectedPath = Path.Combine(_remotePath, fileName);
            txtStatus.Text = "正在备份，请稍候...";
            btnBackup.IsEnabled = false;

            try
            {
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(ConfigHelper.DatabaseSettings.GetConnectionString("master"));
                conn.Open();

                var sql = $"BACKUP DATABASE [{_dbName}] TO DISK = N'{SelectedPath}' WITH COMPRESSION, INIT, NAME = N'{_dbName}-完整数据库备份'";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();

                txtStatus.Text = "备份完成！";
                txtStatus.Foreground = System.Windows.Media.Brushes.Green;
                MessageBox.Show("备份成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (msg.Contains("not support") || msg.Contains("COMPRESSION") || msg.Contains("不支持"))
                {
                    try
                    {
                        using var conn2 = new Microsoft.Data.SqlClient.SqlConnection(ConfigHelper.DatabaseSettings.GetConnectionString("master"));
                        conn2.Open();
                        var sql2 = $"BACKUP DATABASE [{_dbName}] TO DISK = N'{SelectedPath}' WITH INIT, NAME = N'{_dbName}-完整数据库备份'";
                        using var cmd2 = new Microsoft.Data.SqlClient.SqlCommand(sql2, conn2);
                        cmd2.ExecuteNonQuery();

                        txtStatus.Text = "备份完成（未压缩）！";
                        txtStatus.Foreground = System.Windows.Media.Brushes.Green;
                        MessageBox.Show("备份成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        return;
                    }
                    catch (Exception ex2)
                    {
                        txtStatus.Text = "备份失败：" + ex2.Message;
                        txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                        MessageBox.Show("备份失败：" + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    txtStatus.Text = "备份失败：" + msg;
                    txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                    MessageBox.Show("备份失败：" + msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                btnBackup.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
