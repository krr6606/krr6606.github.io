using System.Collections.Generic;
using UnityEngine;
public class CatSpawner : MonoBehaviour
{
    public Transform PoolParnt;
    public GameObject[] isSpawned; // 각 y축 위치에 적이 이미 스폰되었는지 여부를 나타내는 배열
    List<GameObject> catPrefabList;

    public List<ComponentPool> catPoolList;
    public int MaxSpawnCount = 50;
    public bool isSpawnStopped = false;
    public float SpawnFrameDelay = 0.2f;
    void Awake()
    {
       PoolParnt = GameObject.Find("CatUnitPool").GetComponent<Transform>();
    }
    void Start()
    {
        catPrefabList = new List<GameObject>();

        for (int i = 0; i < GameDataManager.Instance.userGameDataSO.CatSlotArray.Length; i++)
        {
            if (GameDataManager.Instance.userGameDataSO.CatSlotArray[i] != CatName.None)
            {
                catPrefabList.Add(GameDataManager.Instance.GetCatPrefab(GameDataManager.Instance.userGameDataSO.CatSlotArray[i]));

            }

        }
        catPoolList = new List<ComponentPool>();

        isSpawned = new GameObject[MaxSpawnCount];
        for (int i = 0; i < catPrefabList.Count; i++)
        {
            catPoolList.Add(new ComponentPool(catPrefabList[i].gameObject, PoolParnt));
        }
    }
    public Unit[] GetAllCats()
    {
        List<Unit> allEnemies = new List<Unit>();
        foreach (var pool in catPoolList)
        {
            allEnemies.AddRange(pool.GetAllActiveUnit());
        }
        return allEnemies.ToArray();
    }
    public void Spawn(CatName catName)
    {

        int activeCount = 0;
        for (int i = 0; i < PoolParnt.childCount; i++)
        {

            if (PoolParnt.GetChild(i).gameObject.activeSelf)
            {
                activeCount++;
            }
        }

        isSpawnStopped = activeCount >= 50;
        if (isSpawnStopped)
        {
            return;
        }
        GameObject cat = null;
        cat = catPoolList[(int)catName - 1].Get();

        if (cat == null)
        {
            return;
        }

        SetPositionY(cat);

    }
    private void SetPositionY(GameObject cat)
    {

        for (int i = 0; i < isSpawned.Length; i++)
        {
            if (isSpawned[i] == null)
            {
                cat.transform.position = new Vector3(transform.position.x, transform.position.y - i * 0.05f, transform.position.z);
                isSpawned[i] = cat;
                cat.GetComponent<SpriteRenderer>().sortingOrder = i;
                break;
            }
        }
    }
}

