using UnityEngine;

namespace TeamSH
{
    // id CategoryType CategoryName
    public class ItemCategoryDTO
    {
        public int Id;
        public CategoryType CategoryType;
        public string CategoryName;
        public string ItemName;
        public string PrefabKey;
    }
    
    [System.Serializable]
    public class ItemCategoryDef
    {
        public int Id;
        public CategoryType CategoryType;
        public string CategoryName;

    }
}