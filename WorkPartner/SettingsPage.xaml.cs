using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WorkPartner
{
    public class TaskColorViewModel
    {
        public string Name { get; set; }
        public string ColorHex { get; set; }
        public SolidColorBrush ColorBrush => (SolidColorBrush)new BrushConverter().ConvertFromString(ColorHex);
    }

    public partial class SettingsPage : UserControl
    {
        public AppSettings Settings { get; set; }

        public SettingsPage()
        {
            InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            UpdateUIFromSettings();
            LoadTaskColors();
            this.DataContext = this; // Set DataContext for bindings in ProcessRegistrationControl
        }

        #region Subject Color Settings
        private void LoadTaskColors()
        {
            if (Settings == null) LoadSettings();

            List<TaskItem> tasks = new List<TaskItem>();
            if (File.Exists(DataManager.TasksFilePath))
            {
                var json = File.ReadAllText(DataManager.TasksFilePath);
                tasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? new List<TaskItem>();
            }

            var taskColorVMs = tasks.Select(task =>
            {
                Settings.TaskColors.TryGetValue(task.Text, out string colorHex);
                return new TaskColorViewModel { Name = task.Text, ColorHex = colorHex ?? "#FFFFFFFF" };
            }).OrderBy(t => t.Name).ToList();

            TaskColorsListBox.ItemsSource = taskColorVMs;
        }

        private void TaskColorsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TaskColorsListBox.SelectedItem is TaskColorViewModel selectedTask)
            {
                var inputWindow = new InputWindow($"'{selectedTask.Name}'의 색상 변경", selectedTask.ColorHex)
                {
                    Owner = Window.GetWindow(this)
                };

                if (inputWindow.ShowDialog() == true)
                {
                    string newColorHex = inputWindow.ResponseText.Trim();
                    try
                    {
                        new BrushConverter().ConvertFromString(newColorHex);
                        Settings.TaskColors[selectedTask.Name] = newColorHex;
                        DataManager.SaveSettingsAndNotify(Settings);
                        LoadTaskColors();
                    }
                    catch (Exception)
                    {
                        System.Windows.MessageBox.Show("잘못된 색상 코드입니다. '#AARRGGBB' 또는 'Red'와 같은 형식으로 입력해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        #endregion

        #region Data Load and Save
        private void LoadSettings()
        {
            Settings = DataManager.LoadSettings();
        }

        private void UpdateUIFromSettings()
        {
            NicknameTextBox.Text = Settings.Nickname;
            IdleDetectionCheckBox.IsChecked = Settings.IsIdleDetectionEnabled;
            IdleTimeoutTextBox.Text = Settings.IdleTimeoutSeconds.ToString();
            MiniTimerCheckBox.IsChecked = Settings.IsMiniTimerEnabled;
            NagMessageTextBox.Text = Settings.FocusModeNagMessage;
            NagIntervalTextBox.Text = Settings.FocusModeNagIntervalSeconds.ToString();
            TagRulesListView.ItemsSource = Settings.TagRules.ToList(); // ToList for display
        }
        #endregion

        #region UI Event Handlers
        private void NicknameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && IsLoaded)
            {
                Settings.Nickname = NicknameTextBox.Text;
                DataManager.SaveSettingsAndNotify(Settings);
            }
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (Settings == null) return;
            if (sender == IdleDetectionCheckBox)
            {
                Settings.IsIdleDetectionEnabled = IdleDetectionCheckBox.IsChecked ?? true;
            }
            else if (sender == MiniTimerCheckBox)
            {
                Settings.IsMiniTimerEnabled = MiniTimerCheckBox.IsChecked ?? false;
                (Application.Current.MainWindow as MainWindow)?.ToggleMiniTimer();
            }
            DataManager.SaveSettingsAndNotify(Settings);
        }

        private void Setting_Changed_IdleTimeout(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && int.TryParse(IdleTimeoutTextBox.Text, out int timeout))
            {
                Settings.IdleTimeoutSeconds = timeout;
                DataManager.SaveSettingsAndNotify(Settings);
            }
        }

        private void NagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.FocusModeNagMessage = NagMessageTextBox.Text;
                DataManager.SaveSettingsAndNotify(Settings);
            }
        }

        private void NagIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && int.TryParse(NagIntervalTextBox.Text, out int interval) && interval > 0)
            {
                Settings.FocusModeNagIntervalSeconds = interval;
                DataManager.SaveSettingsAndNotify(Settings);
            }
        }

        private void AddTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordInput.Text.Trim();
            string tag = TagInput.Text.Trim();
            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(tag))
            {
                System.Windows.MessageBox.Show("키워드와 태그를 모두 입력해주세요.");
                return;
            }

            if (!tag.StartsWith("#")) tag = "#" + tag;
            if (!Settings.TagRules.ContainsKey(keyword))
            {
                Settings.TagRules[keyword] = tag;
                TagRulesListView.ItemsSource = Settings.TagRules.ToList();
                DataManager.SaveSettingsAndNotify(Settings);
                KeywordInput.Clear();
                TagInput.Clear();
            }
            else
            {
                System.Windows.MessageBox.Show("이미 존재하는 키워드입니다.");
            }
        }

        private void DeleteTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagRulesListView.SelectedItem is KeyValuePair<string, string> selectedRule)
            {
                if (System.Windows.MessageBox.Show($"'{selectedRule.Key}' -> '{selectedRule.Value}' 규칙을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Settings.TagRules.Remove(selectedRule.Key);
                    TagRulesListView.ItemsSource = Settings.TagRules.ToList();
                    DataManager.SaveSettingsAndNotify(Settings);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("삭제할 규칙을 목록에서 선택해주세요.");
            }
        }

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    DataManager.DeleteAllData();
                    System.Windows.MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램을 다시 시작해주세요.", "초기화 완료");
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }
        #endregion

        #region Scroll Improvement Logic
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is UIElement element && !e.Handled)
            {
                var scrollViewer = FindVisualParent<ScrollViewer>(element);
                if (scrollViewer != null)
                {
                    if (e.Delta < 0) scrollViewer.LineDown();
                    else scrollViewer.LineUp();
                    e.Handled = true;
                }
            }
        }
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }
        #endregion
    }
}

