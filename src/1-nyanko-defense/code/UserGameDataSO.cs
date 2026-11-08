using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "UserGameDataSO", menuName = "ScriptableObjects/UserGameDataSO", order = 1)]
public class UserGameDataSO : ScriptableObject
{

     [SerializeField] public List<GameLevelData> levelDataList;
    public void LevelUp(GameLevelDataName levelName)
    {
        GameLevelData levelData = levelDataList.Find(data => data.levelName == levelName);
        if (levelData == null) return;
        if(levelData.level >= LevelUpCost_1_10.Length) return;
        levelData.level++;
        

    }

    public StageName maxclearedStage;
    public int currentEXP = 0;
    
    public int[] LevelUpCost_1_10 = new int[10] { 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000 };
    public List<CatName> UnLockList;
    public CatName[] CatSlotArray;
}

[System.Serializable]
public class GameLevelData
{
    public GameLevelDataName levelName;
    public int level = 1;
}