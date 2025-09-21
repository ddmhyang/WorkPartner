using System.Windows;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private MiniTimerWindow miniTimer;

        public MainWindow()
        {
            InitializeComponent();
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

        // 미니 타이머 토글 메서드 추가
        public void ToggleMiniTimer(bool show)
        {
            if (show)
            {
                if (miniTimer == null || !miniTimer.IsVisible)
                {
                    miniTimer = new MiniTimerWindow();
                    miniTimer.Owner = this;
                    miniTimer.Show();
                }
            }
            else
            {
                if (miniTimer != null && miniTimer.IsVisible)
                {
                    miniTimer.Close();
                    miniTimer = null;
                }
            }
        }
    }
}

