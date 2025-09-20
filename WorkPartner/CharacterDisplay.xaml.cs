// CharacterDisplay.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;

        public CharacterDisplay()
        {
            InitializeComponent();
            // UserControl이 로드될 때 캐릭터를 그리도록 수정
            this.Loaded += (s, e) => UpdateCharacter();
        }

        // 외부에서 캐릭터를 새로고침할 때 호출할 public 메서드
        public void UpdateCharacter()
        {
            LoadData();
            RenderCharacter();
        }

        private void LoadData()
        {
            _settings = DataManager.LoadData<AppSettings>(DataManager.SettingsFilePath);
            _fullShopInventory = DataManager.LoadData<List<ShopItem>>(DataManager.ItemsDbFilePath);
        }

        // 캐릭터를 그리는 핵심 로직
        private void RenderCharacter()
        {
            if (_settings == null || _fullShopInventory == null)
            {
                // 데이터가 로드되지 않았으면 아무것도 그리지 않음
                CharacterGrid.Children.Clear();
                return;
            }

            CharacterGrid.Children.Clear();

            // 장착된 아이템들을 Z-Index 순으로 정렬
            var sortedEquippedItems = _settings.EquippedItems
                .Select(pair => _fullShopInventory.FirstOrDefault(i => i.Id == pair.Value))
                .Where(item => item != null && !string.IsNullOrEmpty(item.ImagePath)) // ImagePath가 있는 아이템만 처리
                .OrderBy(item => GetZIndex(item.Type));

            // 색상 아이템 정보 가져오기
            var hairColor = _settings.CustomColors.ContainsKey(ItemType.HairColor) ? (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.HairColor]) : Colors.White;
            var eyeColor = _settings.CustomColors.ContainsKey(ItemType.EyeColor) ? (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.EyeColor]) : Colors.White;
            var clothesColor = _settings.CustomColors.ContainsKey(ItemType.ClothesColor) ? (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.ClothesColor]) : Colors.White;
            var cushionColor = _settings.CustomColors.ContainsKey(ItemType.CushionColor) ? (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.CushionColor]) : Colors.White;

            foreach (var item in sortedEquippedItems)
            {
                try
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(item.ImagePath, UriKind.RelativeOrAbsolute)),
                        Stretch = Stretch.Uniform
                    };

                    // 색상 아이템 적용
                    if (item.Type == ItemType.HairStyle)
                    {
                        image.Effect = new TintColorEffect { TintColor = hairColor };
                    }
                    else if (item.Type == ItemType.EyeShape)
                    {
                        image.Effect = new TintColorEffect { TintColor = eyeColor };
                    }
                    else if (item.Type == ItemType.Clothes)
                    {
                        image.Effect = new TintColorEffect { TintColor = clothesColor };
                    }
                    else if (item.Type == ItemType.Cushion)
                    {
                        image.Effect = new TintColorEffect { TintColor = cushionColor };
                    }


                    Panel.SetZIndex(image, GetZIndex(item.Type));
                    CharacterGrid.Children.Add(image);
                }
                catch (Exception ex)
                {
                    // 이미지 로드 실패 시 로그 기록 (디버깅에 도움)
                    System.Diagnostics.Debug.WriteLine($"이미지 로드 실패: {item.ImagePath}, 오류: {ex.Message}");
                }
            }
        }

        private int GetZIndex(ItemType type)
        {
            // Z-Index 순서 정의 (숫자가 낮을수록 뒤에 그려짐)
            switch (type)
            {
                case ItemType.Background: return 0;
                case ItemType.Cushion: return 5;
                case ItemType.AnimalTail: return 8;
                case ItemType.Clothes: return 10;
                case ItemType.EyeShape: return 15;
                case ItemType.MouthShape: return 16;
                case ItemType.HairStyle: return 20;
                case ItemType.FaceDeco1: return 30;
                case ItemType.FaceDeco2: return 31;
                case ItemType.FaceDeco3: return 32;
                case ItemType.FaceDeco4: return 33;
                case ItemType.AnimalEar: return 40;
                case ItemType.Accessory1: return 41;
                case ItemType.Accessory2: return 42;
                case ItemType.Accessory3: return 43;
                default: return 5; // 색상 아이템 등은 시각적 요소가 없으므로 기본값
            }
        }
    }
}
