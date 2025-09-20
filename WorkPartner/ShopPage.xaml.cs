using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class ShopPage : UserControl
    {
        private AppSettings _settings;
        private List<ShopItem> _shopInventory;

        public ShopPage()
        {
            InitializeComponent();
            this.Loaded += (s, e) => LoadData(); // 페이지가 로드될 때 데이터 로드
        }

        public void LoadData()
        {
            LoadSettings();
            LoadShopInventory();
        }

        private void LoadSettings()
        {
            _settings = DataManager.LoadData<AppSettings>(DataManager.SettingsFilePath);
        }

        private void SaveSettings()
        {
            DataManager.SaveData(DataManager.SettingsFilePath, _settings);
        }

        private void LoadShopInventory()
        {
            try
            {
                var allItems = DataManager.LoadData<List<ShopItem>>(DataManager.ItemsDbFilePath);
                // 상점에서는 가격이 0보다 큰 아이템, 즉 판매용 아이템만 보여줍니다.
                _shopInventory = allItems?.Where(item => item.Price > 0).ToList() ?? new List<ShopItem>();
                ShopItemsListView.ItemsSource = _shopInventory;
            }
            catch (Exception ex)
            {
                // 오류 발생 시 사용자에게 알림
                MessageBox.Show($"상점 아이템을 불러오는 데 실패했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                _shopInventory = new List<ShopItem>();
                ShopItemsListView.ItemsSource = _shopInventory;
            }
        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Guid itemId)
            {
                var itemToBuy = _shopInventory.Find(item => item.Id == itemId);
                if (itemToBuy == null) return;

                if (_settings.OwnedItemIds.Contains(itemId))
                {
                    MessageBox.Show("이미 보유하고 있는 아이템입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_settings.Coins >= itemToBuy.Price)
                {
                    if (MessageBox.Show($"{itemToBuy.Name} 아이템을 {itemToBuy.Price} 코인으로 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        _settings.Coins -= itemToBuy.Price;
                        _settings.OwnedItemIds.Add(itemId);
                        SaveSettings();
                        SoundPlayer.PlayPurchaseSound();

                        // [수정] MainWindow의 메서드를 호출하여 UI 업데이트
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.UpdateCoinDisplay();

                        MessageBox.Show("구매가 완료되었습니다!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("코인이 부족합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
