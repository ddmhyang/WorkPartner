using System;
using System.Linq;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class AnalysisPage : Page
    {
        private DataManager dataManager;

        public AnalysisPage()
        {
            InitializeComponent();
            dataManager = DataManager.Instance;
            LoadAnalysisData();
        }

        private void LoadAnalysisData()
        {
            // 예시: 지난 7일간의 데이터를 분석하여 표시
            var today = DateTime.Today;
            var lastSevenDaysData = Enumerable.Range(0, 7)
                .Select(offset => today.AddDays(-offset))
                .ToDictionary(date => date, date => dataManager.GetLogs(date));

            // 여기서 lastSevenDaysData를 가공하여 차트나 통계 데이터로 만듭니다.
            // (차트 컨트롤이 없으므로 간단한 텍스트로 표시)

            var totalHours = lastSevenDaysData.Values
                .SelectMany(logs => logs)
                .Sum(log => (log.EndTime - log.StartTime).TotalHours);

            TotalFocusTimeTextBlock.Text = $"지난 7일간 총 집중 시간: {totalHours:F2} 시간";
        }
    }
}
