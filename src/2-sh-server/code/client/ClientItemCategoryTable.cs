
using System.Collections.Generic;
using System.IO;
using Jay.FileIO;
using UnityEngine;

namespace TeamSH
{
    // WaveTable은 EnemyDef가 필요하기 때문에
    // 생성시 EnemyTable을 주입반드다.
    public class ItemCategoryTable
    {
        private const string TableName = "Table/category_items.tsv";

        private Dictionary<int, ItemCategoryDef> itemCategoryDefs = new();

        public int Count => itemCategoryDefs.Count;
        public ItemCategoryDef Get(int i) => itemCategoryDefs[i];

        public void Load()
        {
            itemCategoryDefs.Clear();

            string filePath = Path.Combine(Application.streamingAssetsPath, TableName);

            List<ItemCategoryDTO> rows = TSVReader.ReadTable<ItemCategoryDTO>(filePath);
            int count = 0;
            CategoryType lastCategoryType = CategoryType.None;
            foreach (ItemCategoryDTO dto in rows)
            {
                if (dto.CategoryType != lastCategoryType)
                {
                    
                    lastCategoryType = dto.CategoryType;
                    itemCategoryDefs[count] = new ItemCategoryDef()
                    {
                        Id = count,
                        CategoryType = dto.CategoryType,
                        CategoryName = dto.CategoryName,
                    };
                    count++;
                }

            }
        }
    }
}