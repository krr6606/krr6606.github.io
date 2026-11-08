using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }
    public UserGameDataSO userGameDataSO;
    
    
    // CatName과 CatSO를 연결하는 배열, CatName을 인덱스로 사용하여 CatSO를 가져올 수 있음
    public EntitySO[] CatSOArray; 
    public List<GameObject> CatPrefabList;
    public List<GameObject> EnemyPrefabList;
 
    public Dictionary<CatName, int> CatLevelDic; // 반드시 unlock된 CatName만 포함되어야 함

    public Dictionary<GameLevelDataName, int> GameDataLevelDic;
    public int MaxCatSlotCount = 10;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            SetCatSlot(CatName.Cat1, 0);
            CatLevelDic = new Dictionary<CatName, int>();
            GameDataLevelDic = new Dictionary<GameLevelDataName, int>();
            for (int i = 0; i < userGameDataSO.UnLockList.Count; i++)
            {
                CatLevelDic.Add(userGameDataSO.UnLockList[i], CatSOArray[(int)userGameDataSO.UnLockList[i]].unitdata.Level);
            }
            for(int i = 0; i < userGameDataSO.levelDataList.Count; i++)
            {
                GameDataLevelDic.Add(userGameDataSO.levelDataList[i].levelName, userGameDataSO.levelDataList[i].level);
            }
            DontDestroyOnLoad(gameObject);
        }

    }

    public void CatUpgreade(CatName catName)
    {
        if (CatLevelDic.ContainsKey(catName))
        {
            CatLevelDic[catName]++;
        }
        else
        {
            Debug.LogWarning($"Cat {catName} is not unlocked yet.");
        }
    }
    public void UnlockCat(CatName catName)
    {
        if (!userGameDataSO.UnLockList.Contains(catName))
        {
            userGameDataSO.UnLockList.Add(catName);
            CatSOArray[(int)catName].unitdata.Level = 1;
            CatLevelDic.Add(catName, 1);
        }
    }
    public void SetCatSlot(CatName catName, int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < MaxCatSlotCount)
        {
            Debug.Log($"Set Cat {catName} to slot {slotIndex}");
            userGameDataSO.CatSlotArray[slotIndex] = catName;
        }
    }
    public int GetLevelData(GameLevelDataName levelName)
    {
        return GameDataLevelDic.ContainsKey(levelName) ? GameDataLevelDic[levelName] :  -1;
    }
    public GameObject GetCatPrefab(CatName catName)
    {
        int index = (int)catName - 1;
        if (index >= 0 && index < CatPrefabList.Count)
        {
            return CatPrefabList[index];
        }
        else
        {
            Debug.LogWarning($"Cat prefab for {catName} not found.");
            return null;
        }
    }
}