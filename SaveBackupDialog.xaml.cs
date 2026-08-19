using System.Windows;

namespace StudyDemo01
{
    public partial class SaveBackupDialog : Window
    {
        public string SelectedPath { get; private set; } = "";
        private readonly string _dbName;

        public SaveBackupDialog(string dbName)
        {
            InitializeComponent();
            _dbName = dbName;
            txtBackupPath.TextChanged += (s, e) => UpdateFileName();
            UpdateFileName();
        }

        private void UpdateFileName()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileName = _dbName + "_" + timestamp + ".bak";
            txtFileName.Text = "备份文件: " + fileName;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBackupPath.Text))
            {
                System.Windows.MessageBox.Show("请输入备份路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedPath = txtBackupPath.Text.Trim();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}