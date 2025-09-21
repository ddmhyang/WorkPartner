using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WorkPartner
{
    public partial class DashboardPage : Page
    {
        private DispatcherTimer timer;
        private TimeSpan time;
        private DataManager dataManager;
        private WhiteNoiseManager whiteNoiseManager;

        public DashboardPage()
        {
            InitializeComponent();
            dataManager = DataManager.Instance;
            whiteNoiseManager = new WhiteNoiseManager();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            LoadCharacterData();
            LoadWhiteNoiseSettings();
        }

        private void LoadData()
        {
            TodoListView.ItemsSource = dataManager.GetTodos();
            LogListView.ItemsSource = dataManager.GetLogs(DateTime.Today);
            MemoTextBox.Text = dataManager.GetMemo();
            time = TimeSpan.FromMinutes(25); // 기본값 설정
            TimerTextBlock.Text = time.ToString(@"mm\:ss");
        }

        private void LoadCharacterData()
        {
            var characterData = dataManager.GetCharacterData();
            NicknameText.Text = characterData.Nickname;
            LevelText.Text = $"{characterData.Level} ({characterData.Experience}/{characterData.MaxExperience})";
            MoneyText.Text = characterData.Money.ToString();
            // CurrentTaskText.Text는 타이머 상태에 따라 업데이트 됩니다.
            CharacterDisplay.UpdateCharacter(characterData.EquippedItems);
        }

        private void LoadWhiteNoiseSettings()
        {
            var settings = dataManager.GetSoundSettings();
            WaveSlider.Value = settings.ContainsKey("Wave") ? settings["Wave"] : 0;
            ForestSlider.Value = settings.ContainsKey("Forest") ? settings["Forest"] : 0;
            RainSlider.Value = settings.ContainsKey("Rain") ? settings["Rain"] : 0;
            CampfireSlider.Value = settings.ContainsKey("Campfire") ? settings["Campfire"] : 0;

            // 슬라이더 값에 따라 초기 볼륨 설정
            whiteNoiseManager.SetVolume("Wave", WaveSlider.Value);
            whiteNoiseManager.SetVolume("Forest", ForestSlider.Value);
            whiteNoiseManager.SetVolume("Rain", RainSlider.Value);
            whiteNoiseManager.SetVolume("Campfire", CampfireSlider.Value);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (time.TotalSeconds > 0)
            {
                time = time.Add(TimeSpan.FromSeconds(-1));
                TimerTextBlock.Text = time.ToString(@"mm\:ss");
            }
            else
            {
                timer.Stop();
                MessageBox.Show("시간이 종료되었습니다!");
                CurrentTaskText.Text = "휴식 중";
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            timer.Start();
            CurrentTaskText.Text = "작업 중";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            CurrentTaskText.Text = "일시정지";
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            time = TimeSpan.FromMinutes(25);
            TimerTextBlock.Text = time.ToString(@"mm\:ss");
            CurrentTaskText.Text = "휴식 중";
        }

        private void AddTodo_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TodoInput.Text))
            {
                // TodoItem의 속성 이름을 Task로 수정
                dataManager.AddTodo(new TodoItem { Task = TodoInput.Text, IsCompleted = false });
                TodoInput.Clear();
                TodoListView.ItemsSource = dataManager.GetTodos(); // Refresh
                ((ListView)TodoListView).Items.Refresh();
            }
        }

        private void DeleteTodo_Click(object sender, RoutedEventArgs e)
        {
            var selectedTodos = TodoListView.SelectedItems.Cast<TodoItem>().ToList();
            if (selectedTodos.Any())
            {
                foreach (var todo in selectedTodos)
                {
                    dataManager.RemoveTodo(todo);
                }
                TodoListView.ItemsSource = dataManager.GetTodos();
                ((ListView)TodoListView).Items.Refresh();
            }
        }

        private void TodoCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            dataManager.SaveTodos();
        }

        private void MemoTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            dataManager.SaveMemo(MemoTextBox.Text);
        }

        private void CustomizeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Navigate(new AvatarCustomizationPage());
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (whiteNoiseManager == null) return;

            var slider = sender as Slider;
            string soundName = slider.Name.Replace("Slider", "");
            whiteNoiseManager.SetVolume(soundName, slider.Value);
            dataManager.SaveSoundSetting(soundName, slider.Value);
        }
    }
}

