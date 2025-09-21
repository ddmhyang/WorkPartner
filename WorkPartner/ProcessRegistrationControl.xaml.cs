using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WorkPartner
{
    public partial class ProcessRegistrationControl : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(ProcessRegistrationControl), new PropertyMetadata(""));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty ProcessListTypeProperty =
            DependencyProperty.Register("ProcessListType", typeof(string), typeof(ProcessRegistrationControl), new PropertyMetadata(""));

        public string ProcessListType
        {
            get { return (string)GetValue(ProcessListTypeProperty); }
            set { SetValue(ProcessListTypeProperty, value); }
        }

        private AppSettings _settings;
        private List<InstalledProgram> _allPrograms;
        public ObservableCollection<string> Processes { get; set; }

        public ProcessRegistrationControl()
        {
            InitializeComponent();
            this.DataContextChanged += ProcessRegistrationControl_DataContextChanged;
            Loaded += (s, e) =>
            {
                _allPrograms = (Application.Current.MainWindow as MainWindow)?.GetAllPrograms();
            };
        }

        private void ProcessRegistrationControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is SettingsPage settingsPage)
            {
                _settings = settingsPage.Settings;
                if (_settings == null) return;

                switch (ProcessListType)
                {
                    case "Work":
                        Processes = new ObservableCollection<string>(_settings.WorkProcesses);
                        break;
                    case "Passive":
                        Processes = new ObservableCollection<string>(_settings.PassiveProcesses);
                        break;
                    case "Distraction":
                        Processes = new ObservableCollection<string>(_settings.DistractionProcesses);
                        break;
                }
                ProcessListBox.ItemsSource = Processes;
            }
        }

        private void AddProcessButton_Click(object sender, RoutedEventArgs e)
        {
            var newProcess = ProcessInputTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(newProcess) && !Processes.Contains(newProcess))
            {
                Processes.Add(newProcess);
                UpdateSettingsList();
                ProcessInputTextBox.Clear();
            }
        }

        private void DeleteProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string selected)
            {
                Processes.Remove(selected);
                UpdateSettingsList();
            }
        }

        private void SelectRunningAppButton_Click(object sender, RoutedEventArgs e)
        {
            var sortedApps = (Application.Current.MainWindow as MainWindow)?.GetRunningApps();
            if (sortedApps == null || !sortedApps.Any())
            {
                System.Windows.MessageBox.Show("목록에 표시할 실행 중인 프로그램이 없습니다.");
                return;
            }

            var selectionWindow = new AppSelectionWindow(sortedApps) { Owner = Window.GetWindow(this) };
            if (selectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(selectionWindow.SelectedAppKeyword))
            {
                ProcessInputTextBox.Text = selectionWindow.SelectedAppKeyword;
            }
        }

        private void AddActiveTabButton_Click(object sender, RoutedEventArgs e)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();

                if (string.IsNullOrEmpty(activeUrl))
                {
                    System.Windows.MessageBox.Show("웹 브라우저의 주소를 가져오지 못했습니다. 브라우저가 활성화되어 있는지 확인해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    string urlKeyword = new Uri(activeUrl).Host.ToLower();
                    if (!Processes.Contains(urlKeyword))
                    {
                        Processes.Add(urlKeyword);
                        UpdateSettingsList();
                    }
                }
                catch
                {
                    System.Windows.MessageBox.Show("유효한 URL이 아닙니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            timer.Start();
        }

        private void UpdateSettingsList()
        {
            var processList = Processes.ToList();
            switch (ProcessListType)
            {
                case "Work": _settings.WorkProcesses = processList; break;
                case "Passive": _settings.PassiveProcesses = processList; break;
                case "Distraction": _settings.DistractionProcesses = processList; break;
            }
            DataManager.SaveSettingsAndNotify(_settings);
        }

        #region Autocomplete
        private void AutoComplete_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allPrograms == null || ProcessInputTextBox == null || AutoCompletePopup == null) return;

            string searchText = ProcessInputTextBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                AutoCompletePopup.IsOpen = false;
                return;
            }

            var suggestions = _allPrograms
                .Where(p => p.DisplayName.ToLower().Contains(searchText) || p.ProcessName.ToLower().Contains(searchText))
                .OrderBy(p => p.DisplayName)
                .ToList();

            if (suggestions.Any())
            {
                SuggestionListBox.ItemsSource = suggestions;
                AutoCompletePopup.IsOpen = true;
            }
            else
            {
                AutoCompletePopup.IsOpen = false;
            }
        }

        private void SuggestionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionListBox?.SelectedItem is InstalledProgram selectedProgram)
            {
                ProcessInputTextBox.TextChanged -= AutoComplete_TextChanged;
                ProcessInputTextBox.Text = selectedProgram.ProcessName;
                ProcessInputTextBox.TextChanged += AutoComplete_TextChanged;
                AutoCompletePopup.IsOpen = false;
            }
        }

        private void AutoComplete_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (AutoCompletePopup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    SuggestionListBox.Focus();
                    if (SuggestionListBox.Items.Count > 0 && SuggestionListBox.SelectedIndex < SuggestionListBox.Items.Count - 1)
                        SuggestionListBox.SelectedIndex++;
                }
                else if (e.Key == Key.Up)
                {
                    SuggestionListBox.Focus();
                    if (SuggestionListBox.SelectedIndex > 0)
                        SuggestionListBox.SelectedIndex--;
                }
                else if (e.Key == Key.Escape)
                {
                    AutoCompletePopup.IsOpen = false;
                }
                else if (e.Key == Key.Enter && SuggestionListBox.IsFocused && SuggestionListBox.SelectedItem != null)
                {
                    SuggestionListBox_SelectionChanged(SuggestionListBox, null);
                }
            }
        }
        #endregion
    }
}

