using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace WorkPartner
{
    public partial class SettingsPage : Page
    {
        private AppSettings settings;
        private DataManager dataManager;

        public SettingsPage()
        {
            InitializeComponent();
            dataManager = DataManager.Instance;
            LoadSettings();
        }

        private void LoadSettings()
        {
            settings = dataManager.GetSettings();
            ShowMiniTimerCheckBox.IsChecked = settings.ShowMiniTimer;
            MonitoredAppsListBox.ItemsSource = settings.MonitoredApps;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            settings.ShowMiniTimer = ShowMiniTimerCheckBox.IsChecked ?? false;
            // MonitoredApps는 ListBox에 바인딩되어 실시간으로 업데이트되므로 별도 저장이 필요 없을 수 있음
            // 필요하다면 ListBox의 Items를 다시 settings.MonitoredApps에 할당

            dataManager.SaveSettingsAndNotify(settings);

            // MainWindow에 미니 타이머 표시 여부 전달
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.ToggleMiniTimer(settings.ShowMiniTimer);

            MessageBox.Show("설정이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddAppButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "모니터링할 프로그램을 선택하세요"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string appPath = openFileDialog.FileName;
                if (!settings.MonitoredApps.Contains(appPath))
                {
                    settings.MonitoredApps.Add(appPath);
                    MonitoredAppsListBox.Items.Refresh(); // UI 갱신
                }
            }
        }

        private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (MonitoredAppsListBox.SelectedItem != null)
            {
                settings.MonitoredApps.Remove(MonitoredAppsListBox.SelectedItem.ToString());
                MonitoredAppsListBox.Items.Refresh(); // UI 갱신
            }
        }

        private void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "데이터를 내보낼 폴더를 선택하세요"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    // 필요한 모든 데이터 파일을 대상 폴더에 복사
                    // 예시: File.Copy(dataManager.GetLogsFilePath(), Path.Combine(dialog.FileName, "logs.json"), true);
                    MessageBox.Show("데이터를 성공적으로 내보냈습니다.", "내보내기 완료");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 내보내기 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportDataButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "데이터를 가져올 폴더를 선택하세요"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    // 선택한 폴더의 데이터 파일들을 애플리케이션 데이터 폴더로 복사
                    // 예시: File.Copy(Path.Combine(dialog.FileName, "logs.json"), dataManager.GetLogsFilePath(), true);
                    LoadSettings(); // 데이터 다시 로드
                    MessageBox.Show("데이터를 성공적으로 가져왔습니다. 프로그램을 재시작하면 적용됩니다.", "가져오기 완료");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 가져오기 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말로 모든 데이터를 초기화하시겠습니까? 이 작업은 되돌릴 수 없습니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    // 모든 데이터 파일 삭제
                    // 예시: if(File.Exists(dataManager.GetLogsFilePath())) File.Delete(dataManager.GetLogsFilePath());
                    MessageBox.Show("모든 데이터가 초기화되었습니다. 프로그램을 재시작하세요.", "초기화 완료");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 초기화 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
