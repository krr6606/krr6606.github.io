using Jay.FileIO;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TeamSH
{
    public class KeyWordTable
    {
        private const string TableName = "Table/genre_keywords.tsv";
        private Dictionary<int, KeyWordDef> keyWorldDefs = new();

        public int Count => keyWorldDefs.Count;
        public KeyWordDef Get(int i) => keyWorldDefs[i];
        public void Load()
        {
            keyWorldDefs.Clear();
            // Path클래스의 Combine함수를 이용해서 경로를 이어준다.
            string filePath = Path.Combine(Application.streamingAssetsPath, TableName);
            // 전달드린 TSVReader로 해당 테이블과 DTO 클래스 타입을 넘기면
            // 파싱 쌀먹을 할 수 있습니다. 유용히 쓰십쇼
            List<KeyWordDTO> rows = TSVReader.ReadTable<KeyWordDTO>(filePath);
            foreach (KeyWordDTO dto in rows)
            {
                keyWorldDefs[dto.KeywordId] = new KeyWordDef()
                {
                    KeywordId = dto.KeywordId,
                    GenreType = dto.GenreType,
                    GenreName = dto.GenreName,
                    KeywordName = dto.KeywordName,
                };
            }
        }
    }
}
