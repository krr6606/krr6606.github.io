using Jay.FileIO;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TeamSH
{
    public class GenreTable 
    {
        private const string TableName = "Table/genre_keywords.tsv";

        private Dictionary<int, GenreDef> genreDefs = new();

        public int Count => genreDefs.Count;
        public GenreDef Get(int i) => genreDefs[i];

        public void Load()
        {
            genreDefs.Clear();
            string filePath = Path.Combine(Application.streamingAssetsPath, TableName);

            List<GenreDTO> rows = TSVReader.ReadTable<GenreDTO>(filePath);
            int count = 0;
            GenreType lastGenreType = GenreType.None;
            foreach (GenreDTO dto in rows)
            {
                if (dto.GenreType != lastGenreType)
                {

                    lastGenreType = dto.GenreType;
                    genreDefs[count] = new GenreDef()
                    {
                        GenreId = count,
                        GenreType = dto.GenreType,
                        GenreName = dto.GenreName,
                    };
                    count++;
                }

            }
        }
    }
}