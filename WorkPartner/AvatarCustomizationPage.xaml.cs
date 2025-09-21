using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class AvatarCustomizationPage : Page
    {
        private DataManager dataManager;
        private List<ShopItem> allItems;
        private CharacterData characterData;
        private Dictionary<string, string> temporaryEquippedItems;

        public AvatarCustomizationPage()
        {
            InitializeComponent();
            dataManager = DataManager.Instance;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCharacterData();
            LoadAllItems();
            // 초기 카테고리를 '얼굴'로 설정
            FilterItems("Face");
        }

        private void LoadCharacterData()
        {
            characterData = dataManager.GetCharacterData();
            NicknameText.Text = characterData.Nickname;
            MoneyText.Text = characterData.Money.ToString();

            // 원본 장착 아이템을 임시 사전에 복사
            temporaryEquippedItems = new Dictionary<string, string>(characterData.EquippedItems);
            UpdateCharacterPreview();
        }

        private void LoadAllItems()
        {
            allItems = dataManager.GetAllShopItems();
            var ownedItems = characterData.OwnedItems;

            foreach (var item in allItems)
            {
                // 보유 여부 확인
                item.IsOwned = ownedItems.Contains(item.Name);
                // 현재 장착 여부 확인
                item.IsEquipped = temporaryEquippedItems.ContainsValue(item.Name);
            }
        }

        private void FilterItems(string category)
        {
            ItemsContainer.ItemsSource = allItems.Where(item => item.Type == category);
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var category = (sender as Button).Tag.ToString();
            FilterItems(category);
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (sender as Button).DataContext as ShopItem;
            if (selectedItem == null) return;

            if (selectedItem.IsOwned)
            {
                EquipItem(selectedItem);
            }
            else
            {
                // 미리보기로 아이템 장착
                EquipItem(selectedItem);

                // 구매 확인 창 표시
                var alert = new AlertWindow("아이템 구매", $"'{selectedItem.Name}' 아이템을 {selectedItem.Price} 골드에 구매하시겠습니까?", true);
                if (alert.ShowDialog() == true)
                {
                    PurchaseItem(selectedItem);
                }
                else
                {
                    // 구매 취소 시, 원래 장착 아이템으로 되돌림
                    temporaryEquippedItems = new Dictionary<string, string>(characterData.EquippedItems);
                    UpdateEquippedStatus();
                    UpdateCharacterPreview();
                }
            }
        }

        private void PurchaseItem(ShopItem item)
        {
            if (characterData.Money >= item.Price)
            {
                characterData.Money -= item.Price;
                characterData.OwnedItems.Add(item.Name);
                item.IsOwned = true;

                MoneyText.Text = characterData.Money.ToString();

                // 구매 후 아이템 장착 상태 유지
                EquipItem(item);

                new AlertWindow("구매 완료", "아이템을 성공적으로 구매했습니다.", false).ShowDialog();
            }
            else
            {
                new AlertWindow("골드 부족", "골드가 부족하여 아이템을 구매할 수 없습니다.", false).ShowDialog();
                // 구매 실패 시 원래 장착 아이템으로 복구
                temporaryEquippedItems = new Dictionary<string, string>(characterData.EquippedItems);
                UpdateEquippedStatus();
                UpdateCharacterPreview();
            }
        }

        private void EquipItem(ShopItem itemToEquip)
        {
            // 임시 장착 사전에 아이템 추가 또는 교체
            temporaryEquippedItems[itemToEquip.Type] = itemToEquip.Name;

            UpdateEquippedStatus();
            UpdateCharacterPreview();
        }

        private void UpdateEquippedStatus()
        {
            // 모든 아이템의 장착 상태를 현재 임시 사전에 따라 업데이트
            foreach (var item in allItems)
            {
                item.IsEquipped = temporaryEquippedItems.ContainsValue(item.Name);
            }
        }

        private void UpdateCharacterPreview()
        {
            CharacterPreview.UpdateCharacter(temporaryEquippedItems);
        }

        private void ChangeColor_Click(object sender, RoutedEventArgs e)
        {
            // 색상 변경 로직 (ColorPickerWindow 사용)
            var colorPicker = new ColorPickerWindow();
            if (colorPicker.ShowDialog() == true)
            {
                // 색상 적용 로직 추가 (예: CharacterPreview.SetTintColor(colorPicker.SelectedColor))
                // TintColorEffect.cs 와 같은 셰이더 이펙트가 필요할 수 있습니다.
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            // 변경된 장착 정보와 캐릭터 데이터를 저장
            characterData.EquippedItems = new Dictionary<string, string>(temporaryEquippedItems);
            dataManager.SaveCharacterData(characterData);

            new AlertWindow("저장 완료", "변경사항이 저장되었습니다.", false).ShowDialog();

            // 메인 윈도우의 대시보드로 이동
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}
