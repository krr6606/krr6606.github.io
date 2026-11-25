
using System.Collections.Generic;
using System.IO;
using Jay.FileIO;
using UnityEngine;

namespace TeamSH
{
    // WaveTable은 EnemyDef가 필요하기 때문에
    // 생성시 EnemyTable을 주입반드다.
    public class ItemTable
    {
        private const string TableName = "Table/category_items.tsv";

        private Dictionary<int, ItemDef> itemDefs = new();

        public int Count => itemDefs.Count;
        public ItemDef Get(int i) => itemDefs[i];
        
        public List<ItemDef> GetListAll()
        {
            List<ItemDef> newItems = new List<ItemDef>();
            foreach(var item in itemDefs)
            {
                newItems.Add(item.Value);
            }
            return newItems;
        }
        public void Load()
        {
            itemDefs.Clear();
            // Path클래스의 Combine함수를 이용해서 경로를 이어준다.
            string filePath = Path.Combine(Application.streamingAssetsPath, TableName);


            List<ItemDTO> rows = TSVReader.ReadTable<ItemDTO>(filePath);
            foreach (ItemDTO dto in rows)
            {
                // 모든 프리펩은 MartItem을 가지고 있어야함
                MartItem prefab = Resources.Load<MartItem>(dto.PrefabKey);
                itemDefs[dto.ItemId] = new ItemDef()
                {
                    ItemId = dto.ItemId,
                    CategoryType = dto.CategoryType,
                    CategoryName = dto.CategoryName,
                    ItemName = dto.ItemName,
                    PrefabKey = prefab,
                };
            }
        }
    }
}