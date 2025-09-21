using System.Windows;

namespace WorkPartner
{
    public partial class AlertWindow : Window
    {
        /// <summary>
        /// 간단한 알림 창을 생성합니다. (확인 버튼만 표시)
        /// </summary>
        public AlertWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
        }

        /// <summary>
        /// 확인 또는 예/아니오 선택 창을 생성합니다.
        /// </summary>
        public AlertWindow(string title, string message, bool showConfirmation)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;

            if (showConfirmation)
            {
                OkButton.Visibility = Visibility.Collapsed;
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
