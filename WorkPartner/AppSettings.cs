using System.Collections.Generic;
using System.ComponentModel;

namespace WorkPartner
{
    // 설정을 담당하는 클래스입니다. DataManager를 통해 관리됩니다.
    public class AppSettings : INotifyPropertyChanged
    {
        public bool ShowMiniTimer { get; set; }
        public List<string> MonitoredApps { get; set; } = new List<string>();
        public Dictionary<string, string> AppCategories { get; set; } = new Dictionary<string, string>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
