// 파일: SettingsPage.xaml.cs (수정)
// [수정] DataManager 사용, 예외 처리 강화, UI 업데이트 로직 개선
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

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
        private List<InstalledProgram> _allPrograms;
        private string _targetProcessList;
        private bool _isDataLoaded = false;

        public SettingsPage()
        {
            InitializeComponent();
            this.Loaded += SettingsPage_Loaded;

            // Load programs in the background to avoid UI freezing
            LoadProgramsInBackground();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Load settings only once when the page is loaded
            if (!_isDataLoaded)
            {
                LoadSettings();
                UpdateUIFromSettings();
                _isDataLoaded = true;
            }
        }

        private void LoadProgramsInBackground()
        {
            LoadingProgressBar.Visibility = Visibility.Visible;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (s, e) => {
                _allPrograms = GetAllPrograms();
            };
            worker.RunWorkerCompleted += (s, e) => {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                if (e.Error != null)
                {
                    MessageBox.Show($"프로그램 목록을 불러오는 중 오류가 발생했습니다: {e.Error.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            worker.RunWorkerAsync();
        }


        #region 과목별 색상 설정

        private void LoadTaskColors()
        {
            if (Settings == null) LoadSettings();

            var tasks = DataManager.LoadData<List<TaskItem>>(DataManager.TasksFilePath);

            var taskColorVMs = new List<TaskColorViewModel>();
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    string colorHex = "#FFFFFFFF"; // Default to white
                    if (Settings.TaskColors.ContainsKey(task.Text))
                    {
                        colorHex = Settings.TaskColors[task.Text];
                    }
                    taskColorVMs.Add(new TaskColorViewModel { Name = task.Text, ColorHex = colorHex });
                }
            }

            TaskColorsListBox.ItemsSource = taskColorVMs.OrderBy(t => t.Name).ToList();
        }

        private void TaskColorsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TaskColorsListBox.SelectedItem is TaskColorViewModel selectedTask)
            {
                Color initialColor;
                try
                {
                    initialColor = (Color)ColorConverter.ConvertFromString(selectedTask.ColorHex);
                }
                catch
                {
                    initialColor = Colors.White; // Fallback color
                }
                var colorPickerWindow = new ColorPickerWindow(initialColor) { Owner = Window.GetWindow(this) };

                if (colorPickerWindow.ShowDialog() == true)
                {
                    string newColorHex = colorPickerWindow.SelectedColor.ToString();
                    Settings.TaskColors[selectedTask.Name] = newColorHex;
                    DataManager.SaveSettingsAndNotify(Settings);
                    LoadTaskColors();
                }
            }
        }

        #endregion

        #region 데이터 로드 및 저장
        private void LoadSettings()
        {
            Settings = DataManager.LoadSettings();
        }

        private void SaveSettings()
        {
            DataManager.SaveSettingsAndNotify(Settings);
        }

        private void UpdateUIFromSettings()
        {
            if (Settings == null) LoadSettings();

            IdleDetectionCheckBox.IsChecked = Settings.IsIdleDetectionEnabled;
            IdleTimeoutTextBox.Text = Settings.IdleTimeoutSeconds.ToString();
            MiniTimerCheckBox.IsChecked = Settings.IsMiniTimerEnabled;
            WorkProcessListBox.ItemsSource = Settings.WorkProcesses;
            PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses;
            DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses;
            NagMessageTextBox.Text = Settings.FocusModeNagMessage;
            NagIntervalTextBox.Text = Settings.FocusModeNagIntervalSeconds.ToString();
            TagRulesListView.ItemsSource = new ObservableCollection<KeyValuePair<string, string>>(Settings.TagRules); // Use ObservableCollection for dynamic updates
            LoadTaskColors();
        }
        #endregion

        #region 프로그램/아이콘 관련 로직
        private List<InstalledProgram> GetAllPrograms()
        {
            var programs = new Dictionary<string, InstalledProgram>();
            string registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var registryViews = new[] { RegistryView.Registry32, RegistryView.Registry64 };

            foreach (var view in registryViews)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var key = baseKey.OpenSubKey(registryPath))
                    {
                        if (key == null) continue;
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey == null) continue;
                                    var displayName = subkey.GetValue("DisplayName") as string;
                                    var iconPath = subkey.GetValue("DisplayIcon") as string;
                                    var systemComponent = subkey.GetValue("SystemComponent") as int?;

                                    if (!string.IsNullOrWhiteSpace(displayName) && systemComponent != 1)
                                    {
                                        string executablePath = GetExecutablePathFromIconPath(iconPath);
                                        if (string.IsNullOrEmpty(executablePath)) continue;
                                        string processName = System.IO.Path.GetFileNameWithoutExtension(executablePath).ToLower();
                                        if (!programs.ContainsKey(processName))
                                        {
                                            programs[processName] = new InstalledProgram { DisplayName = displayName, ProcessName = processName, IconPath = executablePath };
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 개별 레지스트리 키 접근 오류는 무시하고 계속 진행
                                System.Diagnostics.Debug.WriteLine($"Error reading registry key {subkeyName}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 최상위 레지스트리 키 접근 오류
                    System.Diagnostics.Debug.WriteLine($"Error accessing registry view {view}: {ex.Message}");
                }
            }

            // Add major browsers manually
            string[] browserProcesses = { "chrome", "msedge", "whale", "firefox" };
            foreach (var browser in browserProcesses)
            {
                if (!programs.ContainsKey(browser))
                {
                    programs[browser] = new InstalledProgram { DisplayName = $"{browser.ToUpper()} Browser", ProcessName = browser };
                }
            }


            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var program in programs.Values)
                {
                    if (!string.IsNullOrEmpty(program.IconPath))
                    {
                        try
                        {
                            program.Icon = GetIcon(program.IconPath);
                        }
                        catch
                        {
                            // 아이콘 로드 실패 시 무시
                        }
                    }
                }
            });

            return programs.Values.OrderBy(p => p.DisplayName).ToList();
        }

        private string GetExecutablePathFromIconPath(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return null;
            try
            {
                string executablePath = iconPath.Split(',')[0].Replace("\"", "");
                if (File.Exists(executablePath)) return executablePath;
            }
            catch { /* 파싱 오류 무시 */ }
            return null;
        }

        private BitmapSource GetIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(filePath))
                {
                    return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { return null; }
        }
        #endregion

        #region UI 이벤트 핸들러
        private void SelectRunningAppButton_Click(object sender, RoutedEventArgs e)
        {
            var allRunningApps = new List<InstalledProgram>();
            var addedProcesses = new HashSet<string>();

            var runningProcesses = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero);
            foreach (var process in runningProcesses)
            {
                try
                {
                    string processName = process.ProcessName.ToLower();
                    if (addedProcesses.Contains(processName) || string.IsNullOrEmpty(process.MainModule?.FileName)) continue;

                    allRunningApps.Add(new InstalledProgram
                    {
                        DisplayName = process.MainWindowTitle,
                        ProcessName = processName,
                        Icon = GetIcon(process.MainModule.FileName)
                    });
                    addedProcesses.Add(processName);
                }
                catch (Exception ex)
                {
                    // 일부 프로세스(예: 시스템 프로세스)는 접근이 거부될 수 있습니다.
                    System.Diagnostics.Debug.WriteLine($"Could not access process {process.ProcessName}: {ex.Message}");
                }
            }

            var sortedApps = allRunningApps.OrderBy(p => p.DisplayName).ToList();
            if (!sortedApps.Any())
            {
                MessageBox.Show("목록에 표시할 실행 중인 프로그램이 없습니다.");
                return;
            }

            var selectionWindow = new AppSelectionWindow(sortedApps) { Owner = Window.GetWindow(this) };
            if (selectionWindow.ShowDialog() == true)
            {
                string selectedKeyword = selectionWindow.SelectedAppKeyword;
                if (string.IsNullOrEmpty(selectedKeyword)) return;

                string targetList = (sender as Button)?.Tag as string;
                TextBox targetTextBox = FindAssociatedTextBox(targetList);
                if (targetTextBox != null)
                {
                    targetTextBox.Text = selectedKeyword;
                }
            }
        }

        private void AddActiveTabButton_Click(object sender, RoutedEventArgs e)
        {
            _targetProcessList = (sender as Button)?.Tag as string;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                string activeUrl = null;
                try
                {
                    activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"브라우저 탭 정보 조회 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }


                if (string.IsNullOrEmpty(activeUrl))
                {
                    MessageBox.Show("웹 브라우저의 주소를 가져오지 못했습니다. 브라우저가 활성화되어 있는지 확인해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string urlKeyword;
                try
                {
                    // 'about:blank'와 같은 내부 페이지는 호스트가 없으므로 예외 처리
                    if (Uri.IsWellFormedUriString(activeUrl, UriKind.Absolute))
                    {
                        urlKeyword = new Uri(activeUrl).Host.ToLower();
                    }
                    else
                    {
                        urlKeyword = activeUrl;
                    }
                }
                catch
                {
                    MessageBox.Show("유효한 URL이 아닙니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TextBox targetTextBox = FindAssociatedTextBox(_targetProcessList);
                if (targetTextBox != null)
                {
                    targetTextBox.Text = urlKeyword;
                }
            };
            timer.Start();
        }

        private void AddProcessToList(string process, ObservableCollection<string> list, ListBox listBox)
        {
            if (!string.IsNullOrEmpty(process) && !list.Contains(process))
            {
                list.Add(process);
                DataManager.SaveSettingsAndNotify(Settings);
                listBox.ItemsSource = null; // 강제 새로고침
                listBox.ItemsSource = list;
            }
        }

        private void DeleteProcessFromList(ListBox listBox, ObservableCollection<string> list)
        {
            if (listBox.SelectedItem is string selected)
            {
                list.Remove(selected);
                DataManager.SaveSettingsAndNotify(Settings);
                listBox.ItemsSource = null; // 강제 새로고침
                listBox.ItemsSource = list;
            }
        }

        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessToList(WorkProcessInputTextBox.Text.Trim().ToLower(), Settings.WorkProcesses, WorkProcessListBox);
            WorkProcessInputTextBox.Clear();
        }
        private void AddPassiveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessToList(PassiveProcessInputTextBox.Text.Trim().ToLower(), Settings.PassiveProcesses, PassiveProcessListBox);
            PassiveProcessInputTextBox.Clear();
        }
        private void AddDistractionProcessButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessToList(DistractionProcessInputTextBox.Text.Trim().ToLower(), Settings.DistractionProcesses, DistractionProcessListBox);
            DistractionProcessInputTextBox.Clear();
        }

        private void DeleteWorkProcessButton_Click(object sender, RoutedEventArgs e) => DeleteProcessFromList(WorkProcessListBox, Settings.WorkProcesses);
        private void DeletePassiveProcessButton_Click(object sender, RoutedEventArgs e) => DeleteProcessFromList(PassiveProcessListBox, Settings.PassiveProcesses);
        private void DeleteDistractionProcessButton_Click(object sender, RoutedEventArgs e) => DeleteProcessFromList(DistractionProcessListBox, Settings.DistractionProcesses);

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (Settings == null || !_isDataLoaded) return;
            if (sender == IdleDetectionCheckBox)
            {
                Settings.IsIdleDetectionEnabled = IdleDetectionCheckBox.IsChecked ?? true;
            }
            else if (sender == MiniTimerCheckBox)
            {
                Settings.IsMiniTimerEnabled = MiniTimerCheckBox.IsChecked ?? false;
                (Application.Current.MainWindow as MainWindow)?.ToggleMiniTimer();
            }
            SaveSettings();
        }

        private void Setting_Changed_IdleTimeout(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && _isDataLoaded && int.TryParse(IdleTimeoutTextBox.Text, out int timeout))
            {
                Settings.IdleTimeoutSeconds = timeout;
                SaveSettings();
            }
        }

        private void NagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && _isDataLoaded)
            {
                Settings.FocusModeNagMessage = NagMessageTextBox.Text;
                SaveSettings();
            }
        }

        private void NagIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && _isDataLoaded && int.TryParse(NagIntervalTextBox.Text, out int interval) && interval > 0)
            {
                Settings.FocusModeNagIntervalSeconds = interval;
                SaveSettings();
            }
        }

        private void AddTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordInput.Text.Trim();
            string tag = TagInput.Text.Trim();
            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(tag))
            {
                MessageBox.Show("키워드와 태그를 모두 입력해주세요.");
                return;
            }

            if (!tag.StartsWith("#")) tag = "#" + tag;
            if (!Settings.TagRules.ContainsKey(keyword))
            {
                Settings.TagRules[keyword] = tag;
                TagRulesListView.ItemsSource = null;
                TagRulesListView.ItemsSource = new ObservableCollection<KeyValuePair<string, string>>(Settings.TagRules.ToList()); // [수정] ObservableCollection으로 갱신
                SaveSettings();
                KeywordInput.Clear();
                TagInput.Clear();
            }
            else
            {
                MessageBox.Show("이미 존재하는 키워드입니다.");
            }
        }

        private void DeleteTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagRulesListView.SelectedItem is KeyValuePair<string, string> selectedRule)
            {
                if (MessageBox.Show($"'{selectedRule.Key}' -> '{selectedRule.Value}' 규칙을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Settings.TagRules.Remove(selectedRule.Key);
                    TagRulesListView.ItemsSource = null;
                    TagRulesListView.ItemsSource = new ObservableCollection<KeyValuePair<string, string>>(Settings.TagRules.ToList()); // [수정] ObservableCollection으로 갱신
                    SaveSettings();
                }
            }
            else
            {
                MessageBox.Show("삭제할 규칙을 목록에서 선택해주세요.");
            }
        }

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    // AppData 폴더 경로 가져오기
                    string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");

                    // 삭제할 파일 목록
                    var filesToDelete = new string[]
                    {
                        DataManager.SettingsFilePath,
                        DataManager.TimeLogFilePath,
                        DataManager.TasksFilePath,
                        DataManager.TodosFilePath,
                        DataManager.MemosFilePath,
                        DataManager.ModelFilePath
                    };

                    foreach (var filePath in filesToDelete)
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }

                    // 로그 폴더도 삭제 (선택적)
                    string logDirectory = Path.Combine(appDataFolder, "Logs");
                    if (Directory.Exists(logDirectory))
                    {
                        Directory.Delete(logDirectory, true);
                    }


                    MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램을 다시 시작해주세요.", "초기화 완료");
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }
        #endregion

        #region 자동완성 로직
        private void AutoComplete_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null || _allPrograms == null) return;

            string tag = textBox.Tag.ToString();
            string searchText = textBox.Text.ToLower();

            Popup popup = FindAssociatedPopup(tag);
            ListBox suggestionBox = FindAssociatedSuggestionBox(tag);

            if (popup == null || suggestionBox == null) return;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                popup.IsOpen = false;
                return;
            }

            var suggestions = _allPrograms
                .Where(p => (p.DisplayName != null && p.DisplayName.ToLower().Contains(searchText)) ||
                            (p.ProcessName != null && p.ProcessName.ToLower().Contains(searchText)))
                .OrderBy(p => p.DisplayName)
                .ToList();

            if (suggestions.Any())
            {
                suggestionBox.ItemsSource = suggestions;
                popup.IsOpen = true;
            }
            else
            {
                popup.IsOpen = false;
            }
        }

        private void SuggestionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem == null) return;

            var textBox = FindAssociatedTextBox(listBox.Tag.ToString());
            var popup = FindAssociatedPopup(listBox.Tag.ToString());

            if (textBox != null && popup != null && listBox.SelectedItem is InstalledProgram selectedProgram)
            {
                textBox.TextChanged -= AutoComplete_TextChanged;
                textBox.Text = selectedProgram.ProcessName;
                textBox.TextChanged += AutoComplete_TextChanged;
                popup.IsOpen = false;
            }
        }

        private void AutoComplete_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe == null) return;

            string tag = fe.Tag.ToString();
            var popup = FindAssociatedPopup(tag);
            var suggestionBox = FindAssociatedSuggestionBox(tag);

            if (popup == null || suggestionBox == null) return;

            if (popup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    if (suggestionBox.Items.Count > 0)
                    {
                        suggestionBox.SelectedIndex = (suggestionBox.SelectedIndex + 1) % suggestionBox.Items.Count;
                        suggestionBox.ScrollIntoView(suggestionBox.SelectedItem);
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    if (suggestionBox.Items.Count > 0)
                    {
                        suggestionBox.SelectedIndex = (suggestionBox.SelectedIndex - 1 + suggestionBox.Items.Count) % suggestionBox.Items.Count;
                        suggestionBox.ScrollIntoView(suggestionBox.SelectedItem);
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    popup.IsOpen = false;
                }
                else if (e.Key == Key.Enter && suggestionBox.SelectedItem != null) // selectedItem 확인 추가
                {
                    e.Handled = true;
                    SuggestionListBox_SelectionChanged(suggestionBox, null);
                }
            }
        }

        private TextBox FindAssociatedTextBox(string tag)
        {
            if (tag == "Work") return WorkProcessInputTextBox;
            if (tag == "Passive") return PassiveProcessInputTextBox;
            if (tag == "Distraction") return DistractionProcessInputTextBox;
            return null;
        }

        private Popup FindAssociatedPopup(string tag)
        {
            if (tag == "Work") return WorkAutoCompletePopup;
            if (tag == "Passive") return PassiveAutoCompletePopup;
            if (tag == "Distraction") return DistractionAutoCompletePopup;
            return null;
        }

        private ListBox FindAssociatedSuggestionBox(string tag)
        {
            if (tag == "Work") return WorkSuggestionListBox;
            if (tag == "Passive") return PassiveSuggestionListBox;
            if (tag == "Distraction") return DistractionSuggestionListBox;
            return null;
        }
        #endregion

        #region 스크롤 개선 로직
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
        #endregion
    }
}
