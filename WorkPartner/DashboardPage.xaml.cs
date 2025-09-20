// 파일: DashboardPage.xaml.cs (수정)
// [수정] DataManager의 Load/Save 메서드를 사용하도록 수정
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WorkPartner.AI;
using Microsoft.Win32;
using System.Text;


namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        #region 변수 선언
        public ObservableCollection<TaskItem> TaskItems { get; set; }
        public ObservableCollection<TodoItem> TodoItems { get; set; } // 모든 ToDo 항목
        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; } // 화면에 표시될 ToDo 항목
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; }
        public ObservableCollection<string> SuggestedTags { get; set; }
        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private TimeSpan _totalTimeTodayFromLogs;
        private TimeSpan _selectedTaskTotalTimeFromLogs;
        private MemoWindow _memoWindow;
        private AppSettings _settings;
        private bool _isFocusModeActive = false;
        private DateTime _lastNagTime;
        private TodoItem _lastAddedTodo;
        private TimeLogEntry _lastUnratedSession;
        private MiniTimerWindow _miniTimer;

        // AI 및 미디어 기능용 변수
        private PredictionService _predictionService;
        private MediaPlayer _bgmPlayer;
        private bool _isBgmPlaying = false;
        private DateTime _lastSuggestionTime;
        private DateTime _currentDateForTimeline = DateTime.Today;

        // 자리 비움 감지 관련 변수
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10;

        // 타임라인 드래그 선택 관련 변수
        private bool _isDragging = false;
        private Point _dragStartPoint;

        private Dictionary<string, SolidColorBrush> _taskColors = new Dictionary<string, SolidColorBrush>();
        private List<SolidColorBrush> _colorPalette = new List<SolidColorBrush>
        {
            new SolidColorBrush(Color.FromRgb(255, 182, 193)), // LightPink
            new SolidColorBrush(Color.FromRgb(173, 216, 230)), // LightBlue
            new SolidColorBrush(Color.FromRgb(144, 238, 144)), // LightGreen
            new SolidColorBrush(Color.FromRgb(255, 255, 224)), // LightYellow
            new SolidColorBrush(Color.FromRgb(221, 160, 221)), // Plum
            new SolidColorBrush(Color.FromRgb(255, 218, 185)), // PeachPuff
            new SolidColorBrush(Color.FromRgb(175, 238, 238)), // PaleTurquoise
            new SolidColorBrush(Color.FromRgb(240, 230, 140)), // Khaki
        };
        private int _colorIndex = 0;
        #endregion

        public DashboardPage()
        {
            InitializeComponent();
            InitializeData();
            LoadAllData();
            InitializeTimer();

            _predictionService = new PredictionService();
            _bgmPlayer = new MediaPlayer();
            _lastSuggestionTime = DateTime.MinValue;
            DataManager.SettingsUpdated += OnSettingsUpdated;
        }

        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>();
            TaskListBox.ItemsSource = TaskItems;

            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            SuggestedTags = new ObservableCollection<string>();
            SuggestedTagsItemsControl.ItemsSource = SuggestedTags;
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (string.IsNullOrEmpty(taskName))
                return new SolidColorBrush(Colors.LightGray);

            // _settings 객체가 null일 경우를 대비
            if (_settings?.TaskColors != null && _settings.TaskColors.TryGetValue(taskName, out string colorHex))
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                }
                catch
                {
                    // Fallback to default logic if color string is invalid
                }
            }

            if (!_taskColors.ContainsKey(taskName))
            {
                _taskColors[taskName] = _colorPalette[_colorIndex % _colorPalette.Count];
                _colorIndex++;
            }
            return _taskColors[taskName];
        }

        #region 데이터 저장 / 불러오기
        private void InitializeTimer() { _stopwatch = new Stopwatch(); _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += Timer_Tick; _timer.Start(); }

        public void LoadAllData()
        {
            LoadSettings();
            LoadTasks();
            LoadTodos();
            LoadTimeLogs();
            UpdateCoinDisplay();
            RecalculateAllTotals();
            RenderTimeTable();
        }

        public void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
        }

        private void OnSettingsUpdated()
        {
            _settings = DataManager.LoadSettings();
            // 설정이 변경되면 UI를 다시 그려야 할 수 있습니다.
            RenderTimeTable();
        }

        private void SaveSettings() { DataManager.SaveSettingsAndNotify(_settings); }

        private void LoadTasks()
        {
            var loadedTasks = DataManager.LoadData<ObservableCollection<TaskItem>>(DataManager.TasksFilePath);
            TaskItems.Clear();
            if (loadedTasks != null)
            {
                foreach (var task in loadedTasks)
                {
                    TaskItems.Add(task);
                }
            }
        }

        private void SaveTasks()
        {
            DataManager.SaveData(DataManager.TasksFilePath, TaskItems);
        }

        private void LoadTodos()
        {
            var loadedTodos = DataManager.LoadData<ObservableCollection<TodoItem>>(DataManager.TodosFilePath);
            TodoItems.Clear();
            if (loadedTodos != null)
            {
                foreach (var todo in loadedTodos)
                {
                    TodoItems.Add(todo);
                }
            }
            FilterTodos();
        }

        private void SaveTodos()
        {
            DataManager.SaveData(DataManager.TodosFilePath, TodoItems);
        }

        private void LoadTimeLogs()
        {
            var loadedLogs = DataManager.LoadData<ObservableCollection<TimeLogEntry>>(DataManager.TimeLogFilePath);
            TimeLogEntries.Clear();
            if (loadedLogs != null)
            {
                foreach (var log in loadedLogs)
                {
                    TimeLogEntries.Add(log);
                }
            }
        }
        private void SaveTimeLogs()
        {
            DataManager.SaveData(DataManager.TimeLogFilePath, TimeLogEntries);
        }
        #endregion

        #region UI 이벤트 핸들러
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RecalculateAllTotals();
            RenderTimeTable();
            TodoDatePicker.SelectedDate = DateTime.Today;
        }

        private void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                LoadAllData();
            }
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string newTaskText = TaskInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newTaskText))
            {
                if (TaskItems.Any(t => t.Text.Equals(newTaskText, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                TaskItems.Add(new TaskItem { Text = newTaskText });
                TaskInput.Clear();
                SaveTasks();
            }
        }

        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is TaskItem selectedTask)
            {
                var inputWindow = new InputWindow("과목 이름 수정", selectedTask.Text)
                {
                    Owner = Window.GetWindow(this)
                };

                if (inputWindow.ShowDialog() == true)
                {
                    string newName = inputWindow.ResponseText.Trim();
                    string oldName = selectedTask.Text;

                    if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                    {
                        return;
                    }

                    if (TaskItems.Any(t => t.Text.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show("이미 존재하는 과목 이름입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Update Task in TaskItems
                    var taskToUpdate = TaskItems.FirstOrDefault(t => t.Text == oldName);
                    if (taskToUpdate != null)
                    {
                        taskToUpdate.Text = newName;
                    }

                    // Update all time logs
                    foreach (var log in TimeLogEntries.Where(l => l.TaskText == oldName))
                    {
                        log.TaskText = newName;
                    }

                    // Update color settings
                    if (_settings.TaskColors.ContainsKey(oldName))
                    {
                        var color = _settings.TaskColors[oldName];
                        _settings.TaskColors.Remove(oldName);
                        _settings.TaskColors[newName] = color;
                    }
                    if (_taskColors.ContainsKey(oldName))
                    {
                        var colorBrush = _taskColors[oldName];
                        _taskColors.Remove(oldName);
                        _taskColors[newName] = colorBrush;
                    }

                    // Save everything
                    SaveTasks();
                    SaveTimeLogs();
                    SaveSettings();

                    // Refresh UI
                    TaskListBox.Items.Refresh();
                    RenderTimeTable();
                }
            }
            else
            {
                MessageBox.Show("수정할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void RateSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastUnratedSession != null && sender is Button button)
            {
                int score = int.Parse(button.Tag.ToString());
                _lastUnratedSession.FocusScore = score;
                SessionReviewPanel.Visibility = Visibility.Collapsed;

                var breakWin = new BreakActivityWindow { Owner = Window.GetWindow(this) };
                if (breakWin.ShowDialog() == true)
                {
                    _lastUnratedSession.BreakActivities = breakWin.SelectedActivities;
                }

                SaveTimeLogs();
                _lastUnratedSession = null;
            }
        }
        private void SaveTodos_Event(object sender, RoutedEventArgs e) { if (sender is CheckBox checkBox && checkBox.DataContext is TodoItem todoItem) { if (todoItem.IsCompleted && !todoItem.HasBeenRewarded) { _settings.Coins += 10; todoItem.HasBeenRewarded = true; UpdateCoinDisplay(); SaveSettings(); SoundPlayer.PlayCompleteSound(); } } SaveTodos(); }
        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { var selectedTask = TaskListBox.SelectedItem as TaskItem; if (_currentWorkingTask != selectedTask) { SessionReviewPanel.Visibility = Visibility.Collapsed; if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); } _currentWorkingTask = selectedTask; UpdateSelectedTaskTotalTimeDisplay(); if (_currentWorkingTask != null) CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask.Text}"; } }

        private void TaskInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddTaskButton_Click(sender, e); } }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is TaskItem selectedTask)
            {
                if (_settings.TaskColors.ContainsKey(selectedTask.Text))
                {
                    _settings.TaskColors.Remove(selectedTask.Text);
                    SaveSettings();
                }
                TaskItems.Remove(selectedTask);
                SaveTasks();
            }
        }

        private void AddTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TodoInput.Text)) return;
            var newTodo = new TodoItem
            {
                Text = TodoInput.Text,
                Date = TodoDatePicker.SelectedDate?.Date ?? DateTime.Today
            };

            TodoItems.Add(newTodo);
            _lastAddedTodo = newTodo;
            UpdateTagSuggestions(_lastAddedTodo);
            TodoInput.Clear();
            SaveTodos();
            FilterTodos();
        }

        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                if (MessageBox.Show($"'{selectedTodo.Text}' 할 일을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    RemoveTodoItem(TodoItems, selectedTodo);
                    SaveTodos();
                    FilterTodos();
                }
            }
            else
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.");
            }
        }

        private bool RemoveTodoItem(ObservableCollection<TodoItem> collection, TodoItem itemToRemove)
        {
            if (collection.Contains(itemToRemove))
            {
                collection.Remove(itemToRemove);
                return true;
            }
            foreach (var item in collection)
            {
                if (RemoveTodoItem(item.SubTasks, itemToRemove))
                {
                    return true;
                }
            }
            return false;
        }

        private void TodoTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SaveTodos(); }

        private void AddSubTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.DataContext is TodoItem parentTodo)
            {
                var subTask = new TodoItem
                {
                    Text = "새 하위 작업",
                    Date = parentTodo.Date
                };
                parentTodo.SubTasks.Add(subTask);
                SaveTodos();
            }
        }

        private void EditTodoDateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TodoItem selectedTodo)
            {
                var dateEditWindow = new DateEditWindow(selectedTodo.Date) { Owner = Window.GetWindow(this) };
                if (dateEditWindow.ShowDialog() == true)
                {
                    selectedTodo.Date = dateEditWindow.SelectedDate;
                    SaveTodos();
                    FilterTodos(); // Refresh the view
                }
            }
        }


        private void DeleteTodoMenuItem_Click(object sender, RoutedEventArgs e) { DeleteSelectedTodo(); }

        private void AddTagMenuItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuItem)?.DataContext is TodoItem selectedTodo) { var inputWindow = new InputWindow("추가할 태그를 입력하세요:", "#태그") { Owner = Window.GetWindow(this) }; if (inputWindow.ShowDialog() == true) { string newTag = inputWindow.ResponseText; if (!string.IsNullOrWhiteSpace(newTag) && !selectedTodo.Tags.Contains(newTag)) { selectedTodo.Tags.Add(newTag); SaveTodos(); } } } }
        private void SuggestedTag_Click(object sender, RoutedEventArgs e) { if (_lastAddedTodo != null && sender is Button button) { string tagToAdd = button.Content.ToString(); if (!_lastAddedTodo.Tags.Contains(tagToAdd)) { _lastAddedTodo.Tags.Add(tagToAdd); SuggestedTags.Remove(tagToAdd); SaveTodos(); } } }
        private void AddManualLogButton_Click(object sender, RoutedEventArgs e) { var win = new AddLogWindow(TaskItems) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.NewLogEntry != null) TimeLogEntries.Add(win.NewLogEntry); SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } }
        private void MemoButton_Click(object sender, RoutedEventArgs e) { if (_memoWindow == null || !_memoWindow.IsVisible) { _memoWindow = new MemoWindow { Owner = Window.GetWindow(this) }; _memoWindow.Show(); } else { _memoWindow.Activate(); } }
        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if ((sender as FrameworkElement)?.Tag is TimeLogEntry log) { var win = new AddLogWindow(TaskItems, log) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.IsDeleted) TimeLogEntries.Remove(log); else { log.StartTime = win.NewLogEntry.StartTime; log.EndTime = win.NewLogEntry.EndTime; log.TaskText = win.NewLogEntry.TaskText; log.FocusScore = win.NewLogEntry.FocusScore; } SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } } }
        private void DeleteSelectedTodo() { if (TodoTreeView.SelectedItem is TodoItem selectedTodo) { var parent = FindParent(null, TodoItems, selectedTodo); if (parent != null) { parent.SubTasks.Remove(selectedTodo); } else { TodoItems.Remove(selectedTodo); } SaveTodos(); FilterTodos(); } else { MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요."); } }

        private void BgmPlayButton_Click(object sender, RoutedEventArgs e)
        {
            string bgmFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "whitenoise.mp3");
            if (!File.Exists(bgmFilePath)) { MessageBox.Show("백색 소음 파일을 찾을 수 없습니다.\n'Sounds/whitenoitse.mp3' 경로에 파일을 추가해주세요.", "오류"); return; }
            if (_isBgmPlaying) { _bgmPlayer.Stop(); BgmPlayButton.Content = "백색 소음 재생"; }
            else { _bgmPlayer.Open(new Uri(bgmFilePath)); _bgmPlayer.Play(); BgmPlayButton.Content = "재생 중..."; }
            _isBgmPlaying = !_isBgmPlaying;
        }

        private void TodoDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterTodos();
        }

        private void TodoPrevDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoDatePicker.SelectedDate.HasValue)
            {
                TodoDatePicker.SelectedDate = TodoDatePicker.SelectedDate.Value.AddDays(-1);
            }
        }

        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoDatePicker.SelectedDate.HasValue)
            {
                TodoDatePicker.SelectedDate = TodoDatePicker.SelectedDate.Value.AddDays(1);
            }
        }
        #endregion

        #region 핵심 로직
        public void SetMiniTimerReference(MiniTimerWindow timer)
        {
            _miniTimer = timer;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_stopwatch.IsRunning && _lastUnratedSession != null)
                {
                    SessionReviewPanel.Visibility = Visibility.Collapsed;
                    _lastUnratedSession = null;
                }
                HandleStopwatchMode();
                CheckFocusAndSuggest();
            }
            catch (Exception ex)
            {
                // 타이머 이벤트에서 발생하는 예외는 전역 핸들러가 잡지 못할 수 있으므로, 여기서 직접 처리합니다.
                _timer.Stop(); // 타이머를 멈춰서 반복적인 오류를 방지합니다.
                LogException(ex); // 로깅 메서드를 호출합니다.
                MessageBox.Show($"타이머 실행 중 오류가 발생했습니다: {ex.Message}\n앱을 다시 시작해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogException(Exception ex)
        {
            try
            {
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
            catch { /* 로깅 실패 시 더 이상 할 수 있는 것이 없습니다. */ }
        }

        private void HandleStopwatchMode()
        {
            try
            {
                string activeProcess = ActiveWindowHelper.GetActiveProcessName();
                string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
                string activeTitle = string.IsNullOrEmpty(activeUrl) ? ActiveWindowHelper.GetActiveWindowTitle().ToLower() : activeUrl;

                ActiveProcessDisplay.Text = $"활성: {activeTitle}";
                string keywordToCheck = !string.IsNullOrEmpty(activeUrl) ? activeUrl : activeProcess;

                if (_settings.DistractionProcesses.Any(p => keywordToCheck.Contains(p)))
                {
                    if (_stopwatch.IsRunning || _isPausedForIdle)
                    {
                        LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : (DateTime?)null);
                        _stopwatch.Reset();
                    }
                    _isPausedForIdle = false;
                    CurrentTaskDisplay.Text = "[딴짓 중!]";
                    if (_isFocusModeActive && (DateTime.Now - _lastNagTime).TotalSeconds > _settings.FocusModeNagIntervalSeconds)
                    {
                        var alert = new AlertWindow(_settings.FocusModeNagMessage) { Owner = Application.Current.MainWindow };
                        alert.ShowDialog();
                        _lastNagTime = DateTime.Now;
                    }
                    UpdateLiveTimeDisplays();
                    return;
                }

                bool isTrackable = _settings.WorkProcesses.Any(p => keywordToCheck.Contains(p));
                bool isPassive = _settings.PassiveProcesses.Any(p => keywordToCheck.Contains(p));

                if (isTrackable || isPassive)
                {
                    bool isCurrentlyIdle = _settings.IsIdleDetectionEnabled && !isPassive && ActiveWindowHelper.GetIdleTime().TotalSeconds > _settings.IdleTimeoutSeconds;

                    if (isCurrentlyIdle)
                    {
                        if (_stopwatch.IsRunning)
                        {
                            _stopwatch.Stop();
                            _isPausedForIdle = true;
                            _idleStartTime = DateTime.Now;
                        }
                        else if (_isPausedForIdle)
                        {
                            if ((DateTime.Now - _idleStartTime).TotalSeconds > IdleGraceSeconds)
                            {
                                LogWorkSession(_sessionStartTime.Add(_stopwatch.Elapsed));
                                _stopwatch.Reset();
                                _isPausedForIdle = false;
                            }
                        }
                    }
                    else
                    {
                        if (_isPausedForIdle)
                        {
                            _isPausedForIdle = false;
                            _stopwatch.Start();
                        }
                        else if (!_stopwatch.IsRunning)
                        {
                            if (_currentWorkingTask == null && TaskItems.Any())
                            {
                                TaskListBox.SelectedIndex = 0;
                            }
                            if (_currentWorkingTask != null)
                            {
                                _sessionStartTime = DateTime.Now;
                                _stopwatch.Start();
                            }
                        }
                    }

                    if (_isPausedForIdle)
                    {
                        CurrentTaskDisplay.Text = $"[자리 비움] {_currentWorkingTask?.Text ?? ""}";
                    }
                    else if (_stopwatch.IsRunning)
                    {
                        CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask?.Text ?? "선택된 과목 없음"}";
                    }
                    else
                    {
                        CurrentTaskDisplay.Text = "선택된 과목 없음";
                    }
                }
                else
                {
                    if (_stopwatch.IsRunning || _isPausedForIdle)
                    {
                        LogWorkSession(_isPausedForIdle ? _sessionStartTime.Add(_stopwatch.Elapsed) : (DateTime?)null);
                        _stopwatch.Reset();
                    }
                    _isPausedForIdle = false;
                    CurrentTaskDisplay.Text = "선택된 과목 없음";
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions that might occur during window/process checks
                LogException(ex);
                // Optionally reset state to avoid repeated errors
                _stopwatch.Reset();
                _isPausedForIdle = false;
                CurrentTaskDisplay.Text = "오류 발생";
            }
            finally
            {
                UpdateLiveTimeDisplays();
            }
        }


        private void LogWorkSession(DateTime? endTime = null)
        {
            if (_currentWorkingTask == null || _stopwatch.Elapsed.TotalSeconds < 1)
            {
                _stopwatch.Reset();
                return;
            }
            var entry = new TimeLogEntry
            {
                StartTime = _sessionStartTime,
                EndTime = endTime ?? _sessionStartTime.Add(_stopwatch.Elapsed),
                TaskText = _currentWorkingTask.Text,
                FocusScore = 0
            };
            TimeLogEntries.Insert(0, entry);
            SaveTimeLogs();
            RecalculateAllTotals();
            RenderTimeTable();
            _lastUnratedSession = entry;
            SessionReviewPanel.Visibility = Visibility.Visible;
        }

        private void CheckFocusAndSuggest()
        {
            if ((DateTime.Now - _lastSuggestionTime).TotalSeconds < 60) return;
            if (!_stopwatch.IsRunning || _currentWorkingTask == null)
            {
                if (AiSuggestionTextBlock != null)
                {
                    AiSuggestionTextBlock.Text = "";
                }
                return;
            }

            _lastSuggestionTime = DateTime.Now;

            var input = new ModelInput
            {
                TaskName = _currentWorkingTask.Text,
                DayOfWeek = (float)DateTime.Now.DayOfWeek,
                Hour = (float)DateTime.Now.Hour,
                Duration = 60
            };
            float predictedScore = _predictionService.Predict(input);

            string suggestion = "";
            if (predictedScore > 0)
            {
                if (predictedScore >= 4.0)
                {
                    suggestion = "AI: 지금은 집중력이 최고조에 달할 시간입니다! 가장 어려운 과제를 처리해보세요.";
                    SoundPlayer.PlayNotificationSound();
                }
                else if (predictedScore < 2.5)
                {
                    suggestion = $"AI: 현재 '{_currentWorkingTask.Text}' 작업의 예상 집중도가 낮습니다. 5분간 휴식 후 다시 시작하는 건 어떠신가요?";
                    SoundPlayer.PlayNotificationSound();
                }
            }

            if (AiSuggestionTextBlock != null)
            {
                AiSuggestionTextBlock.Text = suggestion;
            }
        }

        private void UpdateCoinDisplay() { if (_settings != null) { CoinDisplayTextBlock.Text = _settings.Coins.ToString("N0"); } }

        private void UpdateLiveTimeDisplays()
        {
            TimeSpan timeToDisplay;
            if (_stopwatch.IsRunning)
            {
                timeToDisplay = _totalTimeTodayFromLogs + _stopwatch.Elapsed;
                if (_miniTimer != null) _miniTimer.SetRunningStyle();
            }
            else
            {
                timeToDisplay = _totalTimeTodayFromLogs;
                if (_miniTimer != null) _miniTimer.SetStoppedStyle();
            }

            string timeString = timeToDisplay.ToString(@"hh\:mm\:ss");
            MainTimeDisplay.Text = timeString;
            _miniTimer?.UpdateTime(timeString);

            if (_currentWorkingTask != null)
            {
                TimeSpan selectedTaskTime = _selectedTaskTotalTimeFromLogs;
                if (_stopwatch.IsRunning)
                {
                    selectedTaskTime += _stopwatch.Elapsed;
                }
                SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {selectedTaskTime:hh\\:mm\\:ss}";
            }
        }
        private void UpdateTagSuggestions(TodoItem todo) { SuggestedTags.Clear(); if (todo == null) return; var suggestions = new List<string>(); if (_currentWorkingTask != null && !string.IsNullOrWhiteSpace(_currentWorkingTask.Text)) { suggestions.Add($"#{_currentWorkingTask.Text}"); } if (_settings != null && _settings.TagRules != null) { foreach (var rule in _settings.TagRules) { if (todo.Text.ToLower().Contains(rule.Key.ToLower())) { suggestions.Add(rule.Value); } } } foreach (var suggestion in suggestions.Distinct().Except(todo.Tags)) { SuggestedTags.Add(suggestion); } }

        private void UpdateSelectedTaskTotalTimeDisplay()
        {
            if (_currentWorkingTask != null)
            {
                var taskLogs = TimeLogEntries.Where(log => log.TaskText == _currentWorkingTask.Text && log.StartTime.Date == _currentDateForTimeline.Date);
                _selectedTaskTotalTimeFromLogs = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks));
                SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {_selectedTaskTotalTimeFromLogs:hh\\:mm\\:ss}";
            }
            else
            {
                _selectedTaskTotalTimeFromLogs = TimeSpan.Zero;
                SelectedTaskTotalTimeDisplay.Text = "선택 과목 총계: 00:00:00";
            }
        }

        private void RecalculateAllTotals()
        {
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date);
            _totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks));
            UpdateLiveTimeDisplays();
            UpdateSelectedTaskTotalTimeDisplay();
        }

        private void RenderTimeTable()
        {
            TimeTableContainer.Children.Clear();

            var logsForSelectedDate = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourRowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };

                var hourLabel = new TextBlock { Text = $"{hour:00}", Width = 30, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, Foreground = Brushes.Gray };
                hourRowPanel.Children.Add(hourLabel);

                for (int minuteBlock = 0; minuteBlock < 6; minuteBlock++)
                {
                    var blockStartTime = new TimeSpan(hour, minuteBlock * 10, 0);
                    var blockEndTime = blockStartTime.Add(TimeSpan.FromMinutes(10));
                    var blockContainer = new Grid { Width = 60, Height = 20, Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)), Margin = new Thickness(1, 0, 1, 0) };
                    var overlappingLogs = logsForSelectedDate.Where(log => log.StartTime.TimeOfDay < blockEndTime && log.EndTime.TimeOfDay > blockStartTime).ToList();

                    foreach (var logEntry in overlappingLogs)
                    {
                        var segmentStart = logEntry.StartTime.TimeOfDay > blockStartTime ? logEntry.StartTime.TimeOfDay : blockStartTime;
                        var segmentEnd = logEntry.EndTime.TimeOfDay < blockEndTime ? logEntry.EndTime.TimeOfDay : blockEndTime;
                        var segmentDuration = segmentEnd - segmentStart;

                        if (segmentDuration.TotalSeconds <= 0) continue;

                        double totalBlockWidth = blockContainer.Width;
                        double barWidth = (segmentDuration.TotalMinutes / 10.0) * totalBlockWidth;
                        double leftOffset = ((segmentStart - blockStartTime).TotalMinutes / 10.0) * totalBlockWidth;

                        if (barWidth < 1) continue;

                        var coloredBar = new Border
                        {
                            Width = barWidth,
                            Height = blockContainer.Height,
                            Background = GetColorForTask(logEntry.TaskText),
                            CornerRadius = new CornerRadius(2),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(leftOffset, 0, 0, 0),
                            ToolTip = new ToolTip { Content = $"{logEntry.TaskText}\n{logEntry.StartTime:HH:mm} ~ {logEntry.EndTime:HH:mm}\n\n클릭하여 수정 또는 삭제" },
                            Tag = logEntry,
                            Cursor = Cursors.Hand
                        };
                        coloredBar.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;
                        blockContainer.Children.Add(coloredBar);
                    }

                    var blockWithBorder = new Border
                    {
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 0),
                        Child = blockContainer
                    };
                    hourRowPanel.Children.Add(blockWithBorder);
                }
                TimeTableContainer.Children.Add(hourRowPanel);
            }
        }

        private void FilterTodos()
        {
            FilteredTodoItems.Clear();
            if (TodoItems == null) return;
            DateTime selectedDate = TodoDatePicker.SelectedDate?.Date ?? DateTime.Today.Date;

            foreach (var todo in TodoItems.Where(t => t.Date.Date == selectedDate.Date))
            {
                FilteredTodoItems.Add(todo);
            }
        }

        private void PrevDayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(-1);
            TimelineDatePicker.SelectedDate = _currentDateForTimeline;
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = DateTime.Today;
            TimelineDatePicker.SelectedDate = _currentDateForTimeline;
        }

        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(1);
            TimelineDatePicker.SelectedDate = _currentDateForTimeline;
        }

        private void TimelineDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimelineDatePicker.SelectedDate.HasValue && _currentDateForTimeline.Date != TimelineDatePicker.SelectedDate.Value.Date)
            {
                _currentDateForTimeline = TimelineDatePicker.SelectedDate.Value;
                RenderTimeTable();
                RecalculateAllTotals();
            }
        }

        private TodoItem FindParent(TodoItem currentParent, ObservableCollection<TodoItem> items, TodoItem target)
        {
            if (items.Contains(target)) return currentParent;

            foreach (var item in items)
            {
                var found = FindParent(item, item.SubTasks, target);
                if (found != null) return found;
            }
            return null;
        }

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

        #region 타임라인 일괄 수정 로직

        private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(SelectionCanvas);
            Canvas.SetLeft(SelectionRectangle, _dragStartPoint.X);
            Canvas.SetTop(SelectionRectangle, _dragStartPoint.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            SelectionRectangle.Visibility = Visibility.Visible;
            SelectionCanvas.CaptureMouse();
        }

        private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetPosition(SelectionCanvas);
            var x = Math.Min(_dragStartPoint.X, currentPoint.X);
            var y = Math.Min(_dragStartPoint.Y, currentPoint.Y);
            var w = Math.Abs(_dragStartPoint.X - currentPoint.X);
            var h = Math.Abs(_dragStartPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = w;
            SelectionRectangle.Height = h;
        }

        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            SelectionCanvas.ReleaseMouseCapture();
            SelectionRectangle.Visibility = Visibility.Collapsed;

            var endPoint = e.GetPosition(TimeTableContainer); // 기준을 Canvas가 아닌 TimeTableContainer로 변경
            var startPoint = _dragStartPoint;

            // 좌표가 뒤바뀌었을 경우를 대비하여 정규화
            var rect = new Rect(
                Math.Min(startPoint.X, endPoint.X),
                Math.Min(startPoint.Y, endPoint.Y),
                Math.Abs(startPoint.X - endPoint.X),
                Math.Abs(startPoint.Y - endPoint.Y)
            );

            // 시간 계산을 위한 보정
            double totalHeight = TimeTableContainer.ActualHeight;
            if (totalHeight <= 0) return;
            double hourHeight = totalHeight / 24;

            // 선택 영역에 해당하는 시간 범위 계산
            var startTime = _currentDateForTimeline.Date.AddHours(rect.Top / hourHeight);
            var endTime = _currentDateForTimeline.Date.AddHours(rect.Bottom / hourHeight);

            // 선택된 시간 범위 내에 있는 모든 로그를 찾음
            var logsToEdit = TimeLogEntries
                .Where(log => log.StartTime < endTime && log.EndTime > startTime)
                .ToList();

            if (logsToEdit.Any())
            {
                var taskSelectionWindow = new TaskSelectionWindow(TaskItems) { Owner = Window.GetWindow(this) };
                if (taskSelectionWindow.ShowDialog() == true && taskSelectionWindow.SelectedTask != null)
                {
                    string newTaskName = taskSelectionWindow.SelectedTask.Text;
                    foreach (var log in logsToEdit)
                    {
                        log.TaskText = newTaskName;
                    }
                    SaveTimeLogs();
                    RenderTimeTable(); // 변경 사항을 즉시 타임라인에 반영
                }
            }
        }



        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("타임라인에서 마우스로 영역을 드래그하여 수정할 기록들을 선택하세요.", "일괄 수정 안내");
        }
        #endregion

        private void FocusModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isFocusModeActive = !_isFocusModeActive;

            if (_isFocusModeActive)
            {
                FocusModeButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 255));
                FocusModeButton.Foreground = Brushes.White;
                var alert = new AlertWindow("집중 모드가 활성화되었습니다.\n방해 앱으로 등록된 프로그램을 실행하면 경고가 표시됩니다.")
                {
                    Owner = Application.Current.MainWindow
                };
                alert.ShowDialog();
            }
            else
            {
                FocusModeButton.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEF));
                FocusModeButton.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            }
        }
    }
}
