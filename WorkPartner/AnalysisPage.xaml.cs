// 파일: AnalysisPage.xaml.cs (수정)
// [수정] DataManager 사용 및 비동기 로딩, 예외 처리 추가
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using WorkPartner.AI;

namespace WorkPartner
{
    public partial class AnalysisPage : UserControl
    {
        private List<TimeLogEntry> _allTimeLogs;
        private PredictionService _predictionService;

        public SeriesCollection HourAnalysisSeries { get; set; }
        public string[] HourLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        public AnalysisPage()
        {
            InitializeComponent();
            _allTimeLogs = new List<TimeLogEntry>();
            HourAnalysisSeries = new SeriesCollection();
            _predictionService = new PredictionService();
            DataContext = this;
        }

        // [추가] 페이지가 보일 때마다 데이터를 다시 로드하고 분석하도록 이벤트 핸들러 추가
        private async void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                await LoadAndAnalyzeDataAsync();
            }
        }

        public async Task LoadAndAnalyzeDataAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            try
            {
                await Task.Run(() =>
                {
                    _allTimeLogs = DataManager.LoadData<List<TimeLogEntry>>(DataManager.TimeLogFilePath) ?? new List<TimeLogEntry>();
                    Dispatcher.Invoke(() => LoadTasksForPrediction());
                    if (_allTimeLogs.Any())
                    {
                        _predictionService.TrainModel();
                    }
                });

                if (!_allTimeLogs.Any())
                {
                    // 데이터가 없을 경우 UI 초기화
                    TotalWorkTimeTextBlock.Text = "0 시간 0 분";
                    TotalDaysTextBlock.Text = "0 일";
                    MaxConcentrationTimeTextBlock.Text = "0 분";
                    PeakConcentrationHourTextBlock.Text = "-";
                    OverallAverageFocusScoreTextBlock.Text = "평가 데이터 없음";
                    TaskFocusListView.ItemsSource = null;
                    TaskAnalysisListView.ItemsSource = null;
                    HourAnalysisSeries.Clear();
                    HourLabels = new string[24];
                    GoldenTimeSuggestionTextBlock.Text = "아직 기록된 학습 데이터가 없습니다.";
                    WorkRestPatternSuggestionTextBlock.Text = "데이터를 쌓아 당신의 패턴을 분석해보세요!";
                    PredictionResultTextBlock.Text = "데이터가 부족하여 예측할 수 없습니다.";
                    return;
                }

                // UI 업데이트는 UI 스레드에서 수행
                AnalyzeOverallStats();
                AnalyzeFocusScores();
                UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue);
                GenerateFocusBasedAiSuggestion();
                GenerateWorkRestPatternSuggestion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 분석 중 오류가 발생했습니다: {ex.Message}", "분석 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                // 여기에 로그를 남기는 코드를 추가할 수도 있습니다.
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadTasksForPrediction()
        {
            var tasks = DataManager.LoadData<List<TaskItem>>(DataManager.TasksFilePath);
            TaskPredictionComboBox.ItemsSource = tasks;
            if (tasks != null && tasks.Any()) TaskPredictionComboBox.SelectedIndex = 0;

            DayOfWeekPredictionComboBox.ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => ToKoreanDayOfWeek(d));
            DayOfWeekPredictionComboBox.SelectedIndex = (int)DateTime.Today.DayOfWeek;
            HourPredictionComboBox.ItemsSource = Enumerable.Range(0, 24).Select(h => $"{h} 시");
            HourPredictionComboBox.SelectedIndex = DateTime.Now.Hour;
        }

        private void PredictButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskPredictionComboBox.SelectedItem == null ||
               DayOfWeekPredictionComboBox.SelectedItem == null ||
               HourPredictionComboBox.SelectedItem == null)
            {
                MessageBox.Show("모든 예측 조건을 선택해주세요.");
                return;
            }

            var input = new ModelInput
            {
                TaskName = (TaskPredictionComboBox.SelectedItem as TaskItem).Text,
                DayOfWeek = (float)DayOfWeekPredictionComboBox.SelectedIndex,
                Hour = (float)HourPredictionComboBox.SelectedIndex,
                Duration = 60 // 예측은 1시간(60분) 기준으로 고정
            };

            try
            {
                float predictedScore = _predictionService.Predict(input);
                if (predictedScore > 0)
                {
                    PredictionResultTextBlock.Text = $"예상 집중도: {predictedScore:F2} / 5.0";
                }
                else
                {
                    PredictionResultTextBlock.Text = "아직 예측할 데이터가 충분하지 않아요.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"예측 중 오류가 발생했습니다: {ex.Message}", "예측 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                PredictionResultTextBlock.Text = "예측 실패";
            }
        }

        private void LoadTimeLogs()
        {
            _allTimeLogs = DataManager.LoadData<List<TimeLogEntry>>(DataManager.TimeLogFilePath) ?? new List<TimeLogEntry>();
        }

        // ... (나머지 코드는 동일) ...
        private void AnalyzeOverallStats()
        {
            var totalWorkTime = new TimeSpan(_allTimeLogs.Sum(log => log.Duration.Ticks));
            var totalDays = _allTimeLogs.Select(log => log.StartTime.Date).Distinct().Count();
            TotalWorkTimeTextBlock.Text = $"{(int)totalWorkTime.TotalHours} 시간 {totalWorkTime.Minutes} 분";
            TotalDaysTextBlock.Text = $"{totalDays} 일";
            var maxConcentrationTime = _allTimeLogs.Any() ? _allTimeLogs.Max(log => log.Duration) : TimeSpan.Zero;
            MaxConcentrationTimeTextBlock.Text = $"{(int)maxConcentrationTime.TotalMinutes} 분";
            var hourlyWork = _allTimeLogs.GroupBy(log => log.StartTime.Hour).Select(g => new { Hour = g.Key, TotalMinutes = g.Sum(log => log.Duration.TotalMinutes) }).ToList();
            var peakHourData = hourlyWork.OrderByDescending(h => h.TotalMinutes).FirstOrDefault();
            PeakConcentrationHourTextBlock.Text = peakHourData != null ? $"{peakHourData.Hour} 시 ~ {peakHourData.Hour + 1} 시" : "-";
            var chartValues = new ChartValues<double>();
            var labels = new string[24];
            for (int i = 0; i < 24; i++)
            {
                var hourData = hourlyWork.FirstOrDefault(h => h.Hour == i);
                chartValues.Add(hourData?.TotalMinutes ?? 0);
                labels[i] = i.ToString();
            }
            HourAnalysisSeries.Clear();
            HourAnalysisSeries.Add(new ColumnSeries { Title = "작업량", Values = chartValues });
            HourLabels = labels;
            YFormatter = value => value.ToString("N0");
        }

        private void AnalyzeFocusScores()
        {
            var ratedLogs = _allTimeLogs.Where(log => log.FocusScore > 0).ToList();
            if (!ratedLogs.Any())
            {
                OverallAverageFocusScoreTextBlock.Text = "평가 데이터 없음";
                TaskFocusListView.ItemsSource = null;
                return;
            }

            var overallAverage = ratedLogs.Average(log => log.FocusScore);
            OverallAverageFocusScoreTextBlock.Text = $"{overallAverage:F2} / 5.0";

            var taskFocusAnalysis = ratedLogs
                .GroupBy(log => log.TaskText)
                .Select(group => new TaskFocusAnalysisResult
                {
                    TaskName = group.Key,
                    AverageFocusScore = group.Average(log => log.FocusScore),
                    TotalTime = new TimeSpan(group.Sum(log => log.Duration.Ticks))
                })
                .OrderByDescending(result => result.AverageFocusScore)
                .ToList();

            TaskFocusListView.ItemsSource = taskFocusAnalysis;
        }

        private void UpdateTaskAnalysis(DateTime startDate, DateTime endDate)
        {
            var filteredLogs = _allTimeLogs.Where(log => log.StartTime.Date >= startDate.Date && log.StartTime.Date <= endDate.Date).ToList();
            var taskAnalysis = filteredLogs.GroupBy(log => log.TaskText).Select(group => new TaskAnalysisResult { TaskName = group.Key, TotalTime = new TimeSpan(group.Sum(log => log.Duration.Ticks)) }).OrderByDescending(result => result.TotalTime).ToList();
            TaskAnalysisListView.ItemsSource = taskAnalysis;
        }

        private void GenerateFocusBasedAiSuggestion()
        {
            var ratedLogs = _allTimeLogs.Where(log => log.FocusScore > 0).ToList();
            if (ratedLogs.Count < 3) { GoldenTimeSuggestionTextBlock.Text = "집중도 평가 데이터가 더 쌓이면, 당신의 '황금 시간대'를 분석해 드릴게요!"; return; }
            var bestSlot = ratedLogs.GroupBy(log => new { log.StartTime.DayOfWeek, log.StartTime.Hour }).Select(g => new { Day = g.Key.DayOfWeek, Hour = g.Key.Hour, AverageFocusScore = g.Average(log => log.FocusScore) }).OrderByDescending(s => s.AverageFocusScore).FirstOrDefault();
            if (bestSlot == null) return;
            var peakTask = ratedLogs.Where(log => log.StartTime.DayOfWeek == bestSlot.Day && log.StartTime.Hour == bestSlot.Hour).GroupBy(log => log.TaskText).Select(g => new { TaskName = g.Key, TotalDuration = g.Sum(log => log.Duration.TotalSeconds) }).OrderByDescending(t => t.TotalDuration).FirstOrDefault();
            string peakDayStr = ToKoreanDayOfWeek(bestSlot.Day);
            string suggestion = (peakTask != null) ? $"분석 결과, 주로 '{peakDayStr} {bestSlot.Hour}시'에 '{peakTask.TaskName}' 과목을 진행할 때 평균 집중도({bestSlot.AverageFocusScore:F1}점)가 가장 높았습니다!" : $"분석 결과, 주로 '{peakDayStr} {bestSlot.Hour}시'에 가장 높은 집중력(평균 {bestSlot.AverageFocusScore:F1}점)을 보여주셨습니다.";
            GoldenTimeSuggestionTextBlock.Text = suggestion;
        }

        private void GenerateWorkRestPatternSuggestion()
        {
            var ratedLogs = _allTimeLogs.Where(log => log.FocusScore > 0).OrderBy(l => l.StartTime).ToList();
            if (ratedLogs.Count < 5) { WorkRestPatternSuggestionTextBlock.Text = "최적의 작업/휴식 패턴을 분석하기 위한 데이터가 조금 더 필요해요."; return; }
            var patterns = new List<WorkRestPattern>();
            for (int i = 0; i < ratedLogs.Count - 1; i++)
            {
                var currentLog = ratedLogs[i];
                var nextLog = ratedLogs[i + 1];
                TimeSpan restTime = nextLog.StartTime - currentLog.EndTime;
                if (restTime.TotalMinutes > 5 && restTime.TotalMinutes <= 120)
                {
                    patterns.Add(new WorkRestPattern { WorkDurationMinutes = (int)currentLog.Duration.TotalMinutes, RestDurationMinutes = (int)restTime.TotalMinutes, NextSessionFocusScore = nextLog.FocusScore });
                }
            }
            if (!patterns.Any()) { WorkRestPatternSuggestionTextBlock.Text = "규칙적인 휴식 패턴을 분석하는 중입니다. 조금만 더 힘내주세요!"; return; }
            var bestPattern = patterns.GroupBy(p => (int)(p.WorkDurationMinutes / 15)).Select(g => new { WorkGroup = g.Key * 15, BestRest = g.GroupBy(p => (int)(p.RestDurationMinutes / 5)).Select(rg => new { RestGroup = rg.Key * 5, AvgFocus = rg.Average(p => p.NextSessionFocusScore) }).OrderByDescending(rg => rg.AvgFocus).FirstOrDefault() }).Where(x => x.BestRest != null).OrderByDescending(x => x.BestRest.AvgFocus).FirstOrDefault();
            if (bestPattern != null) { WorkRestPatternSuggestionTextBlock.Text = $"AI 분석: 약 {bestPattern.WorkGroup}-{bestPattern.WorkGroup + 15}분 작업 후, {bestPattern.BestRest.RestGroup}-{bestPattern.BestRest.RestGroup + 5}분 휴식했을 때 다음 세션의 집중도가 가장 높았습니다. 이 패턴을 활용해보세요!"; }
        }

        private string ToKoreanDayOfWeek(DayOfWeek day) { switch (day) { case DayOfWeek.Monday: return "월요일"; case DayOfWeek.Tuesday: return "화요일"; case DayOfWeek.Wednesday: return "수요일"; case DayOfWeek.Thursday: return "목요일"; case DayOfWeek.Friday: return "금요일"; case DayOfWeek.Saturday: return "토요일"; case DayOfWeek.Sunday: return "일요일"; default: return string.Empty; } }
        private void TodayButton_Click(object sender, RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.Today, DateTime.Today); }
        private void ThisWeekButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; var dayOfWeek = (int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1; var startDate = today.AddDays(-dayOfWeek); var endDate = startDate.AddDays(6); UpdateTaskAnalysis(startDate, endDate); }
        private void ThisMonthButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; var startDate = new DateTime(today.Year, today.Month, 1); var endDate = startDate.AddMonths(1).AddDays(-1); UpdateTaskAnalysis(startDate, endDate); }
        private void TotalButton_Click(object sender, RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue); }
        private void CustomDateButton_Click(object sender, RoutedEventArgs e) { if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue) { UpdateTaskAnalysis(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value); } else { MessageBox.Show("시작 날짜와 종료 날짜를 모두 선택해주세요."); } }
    }

    //public class TaskAnalysisResult { public string TaskName { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours} 시간 {TotalTime.Minutes} 분"; }
    //public class WorkRestPattern { public int WorkDurationMinutes { get; set; } public int RestDurationMinutes { get; set; } public int NextSessionFocusScore { get; set; } }
    //public class TaskFocusAnalysisResult { public string TaskName { get; set; } public double AverageFocusScore { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours}시간 {TotalTime.Minutes}분"; }
}
