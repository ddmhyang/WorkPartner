using System;
using System.Text.Json.Serialization;

namespace WorkPartner
{
    // 아이템 카테고리 Enum (수정됨)
    public enum ItemType
    {
        Face,
        Hair,
        Top,
        Bottom,
        Shoes,
        Decoration,
        Background,
        // 아래는 색상 지정용 타입 (실제 아이템은 아님)
        HairColor,
        EyeColor,
        ClothesColor,
    }


    public class ShopItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public string ImagePath { get; set; }
        public ItemType Type { get; set; }

        [JsonIgnore]
        public bool IsOwned { get; set; }
        [JsonIgnore]
        public bool IsEquipped { get; set; }
    }
}

