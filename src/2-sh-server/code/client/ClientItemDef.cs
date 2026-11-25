using UnityEngine;

namespace TeamSH
{
    public enum CategoryType
    {
        Food = 0,
        Kitchenware = 1,
        Household = 2,
        Stationery = 3,
        Tool = 4,
        ToyLeisure = 5,
        None

    }
    // ItemId CategoryType    CategoryName ItemName    PrefabKey
    public class ItemDTO
    {
        public int ItemId;
        public CategoryType CategoryType;
        public string CategoryName;
        public string ItemName;
        public string PrefabKey;
    }
    [System.Serializable]
    public class ItemDef
    {
        public int ItemId;
        public CategoryType CategoryType;
        public string CategoryName;
        public string ItemName;
        public MartItem PrefabKey;
    }

}