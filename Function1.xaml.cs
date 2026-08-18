using System.Windows;

namespace StudyDemo01
{
    public partial class Function1 : Window
    {
        public Function1()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner?.Show();
        }
    }
}