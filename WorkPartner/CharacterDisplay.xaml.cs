using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System;

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        // 이 컨트롤은 캐릭터의 각 파츠를 Image 컨트롤로 표시하는 역할을 합니다.
        // XAML 파일에는 각 파츠에 해당하는 Image 컨트롤들이 있어야 합니다.
        // 예: <Image x:Name="FaceImage"/>, <Image x:Name="HairImage"/> 등

        public CharacterDisplay()
        {
            InitializeComponent();
        }

        // 장착된 아이템 목록을 기반으로 캐릭터의 외형을 업데이트합니다.
        public void UpdateCharacter(Dictionary<string, string> equippedItems)
        {
            // 각 아이템 타입에 맞는 이미지를 로드하여 해당하는 Image 컨트롤에 표시
            // equippedItems 딕셔너리에는 {"타입": "아이템이름"} 형식으로 데이터가 들어있습니다.
            // 실제 이미지 경로는 "Images/타입/아이템이름.png"와 같은 규칙을 따른다고 가정합니다.

            foreach (var item in equippedItems)
            {
                var itemType = item.Key; // "Hair", "Face", "Top" 등
                var itemName = item.Value; // "Blue Hair", "Happy Face" 등

                // XAML에서 이름으로 Image 컨트롤 찾기
                var imageControl = this.FindName($"{itemType}Image") as Image;

                if (imageControl != null)
                {
                    try
                    {
                        // 이미지 경로 생성 및 로드
                        string imagePath = $"pack://application:,,,/images/{itemType}/{itemName}.png";
                        imageControl.Source = new BitmapImage(new Uri(imagePath));
                    }
                    catch
                    {
                        // 이미지를 찾지 못할 경우 비워둠
                        imageControl.Source = null;
                    }
                }
            }
        }
    }
}
