using UnityEngine;

namespace TeamSH
{
    public enum GenreType
    {
        Place,
        Job,
        Sport,
        Animal,
        Movie,
        Game,
        Vehicle,
        Country,
        Subject,
        Color,
        Holiday,
        Zodiac,
        None
    }
    public class GenreDTO
    {
        public int Id;
        public GenreType GenreType;
        public string GenreName;
        public string KeywordName;
    }
    [System.Serializable]
    public class GenreDef
    {
        public int GenreId;
        public GenreType GenreType;
        public string GenreName;
    }
}