using System.Windows;

namespace StudyDemo01
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Function1_Click(object sender, RoutedEventArgs e)
        {
            Function1 function1Window = new Function1();
            function1Window.Owner = this;
            this.Hide();
            function1Window.Show();
        }
    }
}