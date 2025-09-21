using System.ComponentModel;

namespace WorkPartner
{
    public class ShopItem : INotifyPropertyChanged
    {
        public int Id { get; set; } // ID 속성 추가
        public string Name { get; set; }
        public string Type { get; set; }
        public int Price { get; set; }
        public string ImagePath { get; set; }

        private bool _isOwned;
        public bool IsOwned
        {
            get { return _isOwned; }
            set
            {
                _isOwned = value;
                OnPropertyChanged(nameof(IsOwned));
            }
        }

        private bool _isEquipped;
        public bool IsEquipped
        {
            get { return _isEquipped; }
            set
            {
                _isEquipped = value;
                OnPropertyChanged(nameof(IsEquipped));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

