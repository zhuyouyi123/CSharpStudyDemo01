using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace StudyDemo01
{
    public partial class RestoreGuideDialog : Window
    {
        public RestoreGuideDialog()
        {
            InitializeComponent();
            txtBakPath.TextChanged += (s, e) => UpdateSql();
            txtDbName.TextChanged += (s, e) => UpdateSql();
            txtDataPath.TextChanged += (s, e) => UpdateSql();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "SQL Server 备份文件 (*.bak)|*.bak|所有文件 (*.*)|*.*",
                Title = "选择 .bak 备份文件"
            };
            if (dialog.ShowDialog() == true)
            {
                txtBakPath.Text = dialog.FileName;

                // 从文件名推断数据库名
                var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                var underscoreIndex = fileName.LastIndexOf('_');
                if (underscoreIndex > 0)
                {
                    var possibleName = fileName.Substring(0, underscoreIndex);
                    // 如果包含时间戳格式（结尾是纯数字），去掉时间戳部分
                    if (possibleName.Length > 14 && possibleName.Substring(possibleName.Length - 14).All(char.IsDigit))
                        possibleName = possibleName.Substring(0, possibleName.Length - 14);
                    txtDbName.Text = possibleName;
                }
                else
                {
                    txtDbName.Text = fileName;
                }

                txtBakInfo.Visibility = Visibility.Visible;
                txtBakInfo.Text = "已选择: " + Path.GetFileName(dialog.FileName) + " (" + FormatFileSize(new FileInfo(dialog.FileName).Length) + ")";
            }
        }

        private void UpdateSql()
        {
            var bakPath = txtBakPath.Text.Trim();
            var dbName = txtDbName.Text.Trim();
            var dataPath = txtDataPath.Text.Trim();

            if (string.IsNullOrEmpty(bakPath) || string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(dataPath))
            {
                txtSql.Text = "-- 请填写以上信息后自动生成还原脚本";
                return;
            }

            // 转义单引号
            var escapedBakPath = bakPath.Replace("'", "''");

            txtSql.Text = "RESTORE DATABASE [" + dbName + "]\n"
                + "FROM DISK = N'" + escapedBakPath + "'\n"
                + "WITH REPLACE,\n"
                + "MOVE '" + dbName + "' TO '" + dataPath.TrimEnd('\\') + "\\" + dbName + ".mdf',\n"
                + "MOVE '" + dbName + "_log' TO '" + dataPath.TrimEnd('\\') + "\\" + dbName + "_log.ldf';";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var sql = txtSql.Text;
            if (string.IsNullOrEmpty(sql) || sql.StartsWith("--"))
            {
                System.Windows.MessageBox.Show("请先填写完整信息生成还原脚本", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Clipboard.SetText(sql);
            System.Windows.MessageBox.Show("还原脚本已复制到剪贴板\n可在 SQL Server Management Studio 或 Navicat 中粘贴执行", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnCopyQuery_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText("SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID('master')");
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1048576) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1073741824) return (bytes / 1048576.0).ToString("F1") + " MB";
            return (bytes / 1073741824.0).ToString("F2") + " GB";
        }
    }
}
