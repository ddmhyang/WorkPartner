using System.Windows;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 앱 시작 시 대시보드 페이지 로드
            MainFrame.Navigate(new DashboardPage());
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new DashboardPage());
        }

        private void AvatarCustomizationButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AvatarCustomizationPage());
        }

        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AnalysisPage());
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage());
        }
    }
}
