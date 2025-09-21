using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace WorkPartner
{
    public partial class AvatarDecorationPage : UserControl, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;
        private List<ShopItem> _filteredShopInventory;
        private ShopItem _temporarilyEquippedItem;

        public List<ShopItem> FilteredShopInventory
        {
            get => _filteredShopInventory;
            set
            {
                _filteredShopInventory = value;
                OnPropertyChanged(nameof(FilteredShopInventory));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public AvatarDecorationPage()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public void LoadData()
        {
            _settings = DataManager.LoadSettings();
            LoadFullInventory();
            UpdateCharacterPanel();
            PopulateCategories();
            _temporarilyEquippedItem = null;
        }

        private void LoadFullInventory()
        {
            if (File.Exists(DataManager.ItemsDbFilePath))
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                _fullShopInventory = JsonSerializer.Deserialize<List<ShopItem>>(json, options) ?? new List<ShopItem>();
            }
            else
            {
                System.Windows.MessageBox.Show("아이템 데이터베이스 파일(items_db.json)을 찾을 수 없습니다.", "오류");
                _fullShopInventory = new List<ShopItem>();
            }
        }

        private void UpdateCharacterPanel()
        {
            NicknameTextBlock.Text = _settings.Nickname;
            CoinTextBlock.Text = _settings.Coins.ToString("N0");
            AvatarPreviewControl.UpdateCharacter();
        }

        private void PopulateCategories()
        {
            var categories = Enum.GetValues(typeof(ItemType))
                                 .Cast<ItemType>()
                                 .Where(t => t < ItemType.HairColor)
                                 .ToList();
            CategoryListBox.ItemsSource = categories;
            if (categories.Any())
            {
                CategoryListBox.SelectedIndex = 0;
            }
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ItemType selectedType)
            {
                var itemsToShow = _fullShopInventory.Where(item => item.Type == selectedType).ToList();
                foreach (var item in itemsToShow)
                {
                    item.IsOwned = _settings.OwnedItemIds.Contains(item.Id) || item.Price == 0;
                    item.IsEquipped = _settings.EquippedItems.ContainsKey(item.Type) && _settings.EquippedItems[item.Type] == item.Id;
                }
                FilteredShopInventory = itemsToShow;
            }
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is ShopItem clickedItem)
            {
                if (!clickedItem.IsOwned)
                {
                    var result = System.Windows.MessageBox.Show($"'{clickedItem.Name}' 아이템을 {clickedItem.Price} 코인으로 구매하시겠습니까?\n\n(취소 시 미리보기만 적용됩니다)", "구매 확인", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (_settings.Coins >= clickedItem.Price)
                        {
                            _settings.Coins -= clickedItem.Price;
                            _settings.OwnedItemIds.Add(clickedItem.Id);
                            clickedItem.IsOwned = true;
                            SoundPlayer.PlayPurchaseSound();
                            EquipItem(clickedItem);
                            DataManager.SaveSettingsAndNotify(_settings);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("코인이 부족합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        _temporarilyEquippedItem = clickedItem;
                        AvatarPreviewControl.UpdateCharacterWithPreview(_temporarilyEquippedItem);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    EquipItem(clickedItem);
                    DataManager.SaveSettingsAndNotify(_settings);
                }

                UpdateCharacterPanel();
                RefreshItemStates();
            }
        }

        private void EquipItem(ShopItem itemToEquip)
        {
            _temporarilyEquippedItem = null;

            if (itemToEquip.IsEquipped)
            {
                _settings.EquippedItems.Remove(itemToEquip.Type);
                itemToEquip.IsEquipped = false;
            }
            else
            {
                if (_settings.EquippedItems.ContainsKey(itemToEquip.Type))
                {
                    var previouslyEquippedId = _settings.EquippedItems[itemToEquip.Type];
                    var previousItem = FilteredShopInventory.FirstOrDefault(i => i.Id == previouslyEquippedId);
                    if (previousItem != null) previousItem.IsEquipped = false;
                }
                _settings.EquippedItems[itemToEquip.Type] = itemToEquip.Id;
                itemToEquip.IsEquipped = true;
            }
        }

        private void RefreshItemStates()
        {
            if (FilteredShopInventory == null) return;
            foreach (var item in FilteredShopInventory)
            {
                item.IsEquipped = _settings.EquippedItems.ContainsKey(item.Type) && _settings.EquippedItems[item.Type] == item.Id;
            }
            ItemsListView.Items.Refresh();
        }

        private void MyColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (MyColorPicker.SelectedColor.HasValue)
            {
                _settings.CustomColors[ItemType.ClothesColor] = MyColorPicker.SelectedColor.Value.ToString();
                DataManager.SaveSettingsAndNotify(_settings);
                AvatarPreviewControl.UpdateCharacter();
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                LoadData();
            }
            else
            {
                AvatarPreviewControl.UpdateCharacter();
            }
        }
    }
}

