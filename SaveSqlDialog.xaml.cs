using System.Windows;
using System.Windows.Input;

namespace StudyDemo01
{
    public partial class SaveSqlDialog : Window
    {
        public string SqlName { get; private set; } = "";

        public SaveSqlDialog()
        {
            InitializeComponent();
            txtName.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) BtnOk_Click(s, e);
                if (e.Key == Key.Escape) BtnCancel_Click(s, e);
            };
            Loaded += (s, e) => txtName.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            var name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SqlName = name;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}