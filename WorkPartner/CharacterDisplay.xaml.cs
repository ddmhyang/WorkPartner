using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        }

        private void LoadData()
        {
            _settings = DataManager.LoadSettings();
            if (File.Exists(DataManager.ItemsDbFilePath))
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                _fullShopInventory = JsonSerializer.Deserialize<List<ShopItem>>(json, options) ?? new List<ShopItem>();
            }
            else
            {
                _fullShopInventory = new List<ShopItem>();
            }
        }

        public void UpdateCharacter()
        {
            UpdateCharacterWithPreview(null);
        }

        public void UpdateCharacterWithPreview(ShopItem previewItem)
        {
            LoadData();
            CharacterCanvas.Children.Clear();

            var equippedItems = new Dictionary<ItemType, Guid>(_settings.EquippedItems);
            if (previewItem != null)
            {
                equippedItems[previewItem.Type] = previewItem.Id;
            }

            var itemTypesInOrder = new List<ItemType>
            {
                ItemType.Background,
                ItemType.Bottom,
                ItemType.Top,
                ItemType.Shoes,
                ItemType.Hair,
                ItemType.Face,
                ItemType.Decoration
            };

            foreach (var type in itemTypesInOrder)
            {
                Guid itemId;
                if (!equippedItems.TryGetValue(type, out itemId))
                {
                    var defaultItem = _fullShopInventory.FirstOrDefault(i => i.Type == type && i.Price == 0);
                    if (defaultItem == null) continue;
                    itemId = defaultItem.Id;
                }

                var item = _fullShopInventory.FirstOrDefault(i => i.Id == itemId);
                if (item != null)
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(item.ImagePath, UriKind.RelativeOrAbsolute)),
                        Stretch = Stretch.Uniform
                    };

                    if (type == ItemType.Top || type == ItemType.Bottom)
                    {
                        if (_settings.CustomColors.TryGetValue(ItemType.ClothesColor, out var colorHex))
                        {
                            var effect = new TintColorEffect();
                            effect.TintColor = (Color)ColorConverter.ConvertFromString(colorHex);
                            image.Effect = effect;
                        }
                    }

                    CharacterCanvas.Children.Add(image);
                }
            }
        }
    }
}
