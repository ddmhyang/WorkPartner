using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;


namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;
        private AvatarDecorationPage _avatarDecorationPage;
        private MiniTimerWindow _miniTimerWindow;
        private List<InstalledProgram> _allPrograms;


        public MainWindow()
        {
            InitializeComponent();
            DataManager.PrepareFileForEditing("FocusPredictionModel.zip");

            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();
            _avatarDecorationPage = new AvatarDecorationPage();

            PageContent.Content = _dashboardPage;
            UpdateNavButtonSelection(DashboardButton);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ToggleMiniTimer();
            // Load programs in the background
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (s, args) => { _allPrograms = GetAllProgramsInternal(); };
            worker.RunWorkerCompleted += (s, args) => { };
            worker.RunWorkerAsync();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _miniTimerWindow?.Close();
        }

        private void UpdateNavButtonSelection(Button selectedButton)
        {
            DashboardButton.Tag = "Inactive";
            AnalysisButton.Tag = "Inactive";
            AvatarButton.Tag = "Inactive";
            SettingsButton.Tag = "Inactive";

            if (selectedButton != null)
            {
                selectedButton.Tag = "Active";
            }
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            _dashboardPage.LoadAllData();
            PageContent.Content = _dashboardPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _settingsPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            _analysisPage.LoadAndAnalyzeData();
            PageContent.Content = _analysisPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            _avatarDecorationPage.LoadData();
            PageContent.Content = _avatarDecorationPage;
            UpdateNavButtonSelection(sender as Button);
        }

        public void NavigateToAvatarPage()
        {
            AvatarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        public void ToggleMiniTimer()
        {
            var settings = DataManager.LoadSettings();
            if (settings.IsMiniTimerEnabled)
            {
                if (_miniTimerWindow == null || !_miniTimerWindow.IsVisible)
                {
                    _miniTimerWindow = new MiniTimerWindow();
                    _miniTimerWindow.Show();
                    _dashboardPage?.SetMiniTimerReference(_miniTimerWindow);
                }
            }
            else
            {
                _miniTimerWindow?.Close();
                _miniTimerWindow = null;
                _dashboardPage?.SetMiniTimerReference(null);
            }
        }

        #region Program List Logic (Moved to MainWindow)
        public List<InstalledProgram> GetAllPrograms() => _allPrograms;

        public List<InstalledProgram> GetRunningApps()
        {
            var allRunningApps = new List<InstalledProgram>();
            var addedProcesses = new HashSet<string>();

            var runningProcesses = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero);
            foreach (var process in runningProcesses)
            {
                try
                {
                    string processName = process.ProcessName.ToLower();
                    if (addedProcesses.Contains(processName) || new[] { "chrome", "msedge", "whale", "applicationframehost" }.Contains(processName)) continue;

                    string filePath = process.MainModule.FileName;
                    allRunningApps.Add(new InstalledProgram
                    {
                        DisplayName = process.MainWindowTitle,
                        ProcessName = processName,
                        Icon = GetIcon(filePath),
                        IconPath = filePath
                    });
                    addedProcesses.Add(processName);
                }
                catch { }
            }
            return allRunningApps.OrderBy(p => p.DisplayName).ToList();
        }

        private List<InstalledProgram> GetAllProgramsInternal()
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
                            using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                            {
                                if (subkey == null) continue;
                                var displayName = subkey.GetValue("DisplayName") as string;
                                var iconPath = subkey.GetValue("DisplayIcon") as string;
                                var systemComponent = subkey.GetValue("SystemComponent") as int?;

                                if (!string.IsNullOrWhiteSpace(displayName) && systemComponent != 1)
                                {
                                    string executablePath = iconPath?.Split(',')[0].Replace("\"", "");
                                    if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath)) continue;

                                    string processName = Path.GetFileNameWithoutExtension(executablePath).ToLower();
                                    if (!programs.ContainsKey(processName))
                                    {
                                        programs[processName] = new InstalledProgram { DisplayName = displayName, ProcessName = processName, IconPath = executablePath };
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var program in programs.Values)
                {
                    program.Icon = GetIcon(program.IconPath);
                }
            });

            return programs.Values.OrderBy(p => p.DisplayName).ToList();
        }

        private BitmapSource GetIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try
            {
                using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath))
                {
                    return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { return null; }
        }
        #endregion
    }
}

