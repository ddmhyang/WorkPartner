// 파일: ClosetPage.xaml.cs (수정)
// [수정] DataManager의 Load/Save 메서드를 사용하도록 수정하고, 키 타입을 ItemType으로 변경
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;

namespace WorkPartner
{
    public partial class ClosetPage : UserControl
    {
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;

        public ClosetPage()
        {
            InitializeComponent();
            this.IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    LoadData();
                }
            };
        }

        public void LoadData()
        {
            LoadSettings();
            LoadFullInventory();
            PopulateCategories();
            UpdateCharacterPreview();
        }

        private void LoadSettings()
        {
            _settings = DataManager.LoadData<AppSettings>(DataManager.SettingsFilePath);
        }

        private void SaveSettings()
        {
            DataManager.SaveData(DataManager.SettingsFilePath, _settings);
        }

        private void LoadFullInventory()
        {
            _fullShopInventory = DataManager.LoadData<List<ShopItem>>(DataManager.ItemsDbFilePath) ?? new List<ShopItem>();
        }

        private void PopulateCategories()
        {
            var categories = Enum.GetValues(typeof(ItemType)).Cast<ItemType>().ToList();
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
                ItemsListView.ItemsSource = itemsToShow;

                if (IsColorCategory(selectedType))
                {
                    CustomColorPicker.Visibility = Visibility.Visible;
                    LoadCustomColorToPicker(selectedType);
                }
                else
                {
                    CustomColorPicker.Visibility = Visibility.Collapsed;
                }
                UpdateItemButtonsState();
            }
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Guid itemId)
            {
                var clickedItem = _fullShopInventory.FirstOrDefault(item => item.Id == itemId);
                if (clickedItem == null) return;

                if (!_settings.OwnedItemIds.Contains(itemId) && clickedItem.Price > 0)
                {
                    System.Windows.MessageBox.Show("아직 보유하지 않은 아이템입니다. 상점에서 먼저 구매해주세요!", "알림");
                    return;
                }

                // 색상 아이템이 아닌 경우에만 장착/해제 로직 수행
                if (!IsColorCategory(clickedItem.Type))
                {
                    // 현재 아이템이 이미 장착된 상태인지 확인
                    if (_settings.EquippedItems.ContainsKey(clickedItem.Type) && _settings.EquippedItems[clickedItem.Type] == itemId)
                    {
                        // 장착 해제
                        _settings.EquippedItems.Remove(clickedItem.Type);
                    }
                    else
                    {
                        // 장착
                        _settings.EquippedItems[clickedItem.Type] = itemId;
                    }

                    SaveSettings();
                    UpdateCharacterPreview();
                    UpdateItemButtonsState();
                }
            }
        }


        private void MyColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (CategoryListBox.SelectedItem is ItemType selectedType && MyColorPicker.SelectedColor.HasValue)
            {
                if (!IsColorCategory(selectedType)) return;
                // [수정] Dictionary의 키를 string 대신 ItemType으로 사용
                _settings.CustomColors[selectedType] = MyColorPicker.SelectedColor.Value.ToString();
                SaveSettings();
                UpdateCharacterPreview();
            }
        }

        private void LoadCustomColorToPicker(ItemType type)
        {
            // [수정] Dictionary의 키를 string 대신 ItemType으로 사용
            if (_settings.CustomColors.ContainsKey(type))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[type]);
                    MyColorPicker.SelectedColor = color;
                }
                catch
                {
                    MyColorPicker.SelectedColor = Colors.White; // 기본값
                }
            }
            else
            {
                MyColorPicker.SelectedColor = Colors.White;
            }
        }

        private void UpdateItemButtonsState()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ItemsListView.ItemsSource == null) return; // 아이템 목록이 비어있으면 종료

                foreach (var item in ItemsListView.Items)
                {
                    var container = ItemsListView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container == null) continue;
                    var button = FindVisualChild<Button>(container);
                    if (button == null || !(button.Tag is Guid itemId)) continue;
                    var shopItem = _fullShopInventory.FirstOrDefault(i => i.Id == itemId);
                    if (shopItem == null) continue;

                    bool isOwned = _settings.OwnedItemIds.Contains(itemId) || shopItem.Price == 0;
                    // [수정] Dictionary의 키를 string 대신 ItemType으로 사용
                    bool isEquipped = !IsColorCategory(shopItem.Type) && _settings.EquippedItems.ContainsKey(shopItem.Type) && _settings.EquippedItems[shopItem.Type] == itemId;

                    button.IsEnabled = isOwned; // 구매하지 않은 아이템은 비활성화
                    button.Opacity = isOwned ? 1.0 : 0.4; // 비활성화 시 시각적 표시

                    if (isEquipped)
                    {
                        button.BorderBrush = Brushes.Gold;
                        button.BorderThickness = new Thickness(3);
                    }
                    else
                    {
                        button.BorderBrush = SystemColors.ControlDarkBrush;
                        button.BorderThickness = new Thickness(1);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private bool IsColorCategory(ItemType type)
        {
            return type == ItemType.HairColor ||
                   type == ItemType.EyeColor ||
                   type == ItemType.ClothesColor ||
                   type == ItemType.CushionColor;
        }

        private void UpdateCharacterPreview()
        {
            CharacterPreviewControl.UpdateCharacter();
        }

        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
    }
}
