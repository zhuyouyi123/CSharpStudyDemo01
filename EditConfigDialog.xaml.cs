using System.Windows;

namespace StudyDemo01
{
    public partial class EditConfigDialog : Window
    {
        public bool IsSaved { get; private set; }

        public EditConfigDialog()
        {
            InitializeComponent();
            LoadCurrentConfig();
        }

        private void LoadCurrentConfig()
        {
            var config = ConfigHelper.DatabaseSettings;
            txtServer.Text = config.Server;
            txtPort.Text = config.Port.ToString();
            txtUserId.Text = config.UserId;
            txtPassword.Password = config.Password;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtServer.Text))
            {
                System.Windows.MessageBox.Show("请输入服务器地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPort.Text) || !int.TryParse(txtPort.Text, out _))
            {
                System.Windows.MessageBox.Show("请输入正确的端口号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtUserId.Text))
            {
                System.Windows.MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = new DatabaseConfig
            {
                Server = txtServer.Text.Trim(),
                Port = int.Parse(txtPort.Text.Trim()),
                Database = "master",
                UserId = txtUserId.Text.Trim(),
                Password = txtPassword.Password,
                TrustServerCertificate = true,
                ConnectTimeout = 15
            };

            try
            {
                ConfigHelper.Save(config);
                IsSaved = true;
                System.Windows.MessageBox.Show("配置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}