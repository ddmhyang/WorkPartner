using System.Collections.Generic;

namespace WorkPartner
{
    public class CharacterData
    {
        public string Nickname { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int MaxExperience { get; set; }
        public int Money { get; set; }
        public List<string> OwnedItems { get; set; }
        public Dictionary<string, string> EquippedItems { get; set; }

        public CharacterData()
        {
            // 기본값 설정
            Nickname = "플레이어";
            Level = 1;
            Experience = 0;
            MaxExperience = 100;
            Money = 1000;
            OwnedItems = new List<string>();
            EquippedItems = new Dictionary<string, string>
            {
                { "Face", "기본 얼굴" },
                { "Hair", "기본 헤어" },
                { "Top", "기본 상의" },
                { "Bottom", "기본 하의" },
                { "Shoes", "기본 신발" }
            };
        }
    }
}
