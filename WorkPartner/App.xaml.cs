using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Threading;

namespace WorkPartner
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        // Mutex 객체를 앱 전체에서 사용할 수 있도록 멤버 변수로 선언합니다.
        // "WorkPartnerMutex"는 우리 앱만의 고유한 이름입니다. 아무거나 상관없어요.
        private static Mutex mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "WorkPartnerMutex";
            bool createdNew;

            // appName이라는 이름으로 Mutex를 요청합니다.
            // createdNew 변수는 깃발을 새로 꽂았는지(true) 아닌지(false) 알려줍니다.
            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // 깃발을 새로 꽂지 못했다면, 이미 앱이 실행 중이라는 의미입니다.
                // 사용자에게 알리고 현재 시작하려는 앱은 종료합니다.
                MessageBox.Show("WorkPartner가 이미 실행 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return; // Ensure the application exits immediately
            }

            base.OnStartup(e);

            // [추가] 전역 예외 처리기 설정
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // [추가] 사용자에게 친절한 오류 메시지 표시
            MessageBox.Show("죄송합니다. 예상치 못한 오류가 발생했습니다.\n자세한 내용은 로그 파일을 확인해주세요.", "오류 발생", MessageBoxButton.OK, MessageBoxImage.Error);

            // [추가] 오류 정보를 로그 파일에 기록
            LogException(e.Exception);

            // [추가] 프로그램이 비정상 종료되는 것을 방지
            e.Handled = true;
        }

        private void LogException(Exception ex)
        {
            try
            {
                // AppData 폴더에 로그를 저장하여 권한 문제를 피합니다.
                string logDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner", "Logs");
                Directory.CreateDirectory(logDirectory);
                string logFilePath = System.IO.Path.Combine(logDirectory, "error_log.txt");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("====================================================");
                sb.AppendLine($"Timestamp: {DateTime.Now}");
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    sb.AppendLine("--- Inner Exception ---");
                    sb.AppendLine($"Type: {ex.InnerException.GetType().FullName}");
                    sb.AppendLine($"Message: {ex.InnerException.Message}");
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(ex.InnerException.StackTrace);
                }
                sb.AppendLine("====================================================");
                sb.AppendLine();

                File.AppendAllText(logFilePath, sb.ToString());
            }
            catch (Exception logEx)
            {
                // 로깅 자체에서 오류가 발생할 경우를 대비
                MessageBox.Show($"로그 파일 작성에 실패했습니다: {logEx.Message}", "로깅 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
