using System.Collections.Generic;
using UnityEngine;



public class ComponentPool: Component
{
    private readonly GameObject original;       // 복제 원본 (프리팹 또는 씬 개체)
    private readonly Transform parent; // 복제본을 붙일 부모 (null이면 루트)

    // 지금까지 만든 복제본들. 켜짐/꺼짐 여부로 사용 중인지 구분한다.
    private readonly List<GameObject> spawned = new List<GameObject>();

    public ComponentPool(GameObject original, Transform parent = null)
    {
        this.original = original;
        this.parent = parent;
    }

    /// <summary>
    /// 사용 가능한 개체를 하나 꺼내온다. (켜진 상태로 반환됨)
    /// </summary>
    public GameObject Get()
    {
        // 1. 먼저 꺼져 있는(재사용 가능한) 복제본이 있는지 찾아본다.
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i].activeSelf == false)
            {
                spawned[i].SetActive(true);
                return spawned[i];
            }
        }

        // 2. 재사용할 것이 없을 때만 새로 만든다.
        GameObject newOne = GameObject.Instantiate(original, parent);
        spawned.Add(newOne);
        newOne.SetActive(true);
        return newOne;
    }
    public Unit[] GetAllActiveUnit()
    {
        List<Unit> activeUnits = new List<Unit>();
        foreach (var obj in spawned)
        {
            if (obj.activeSelf)
            {
                Unit unitComponent = obj.GetComponent<Unit>();
                if (unitComponent != null)
                {
                    activeUnits.Add(unitComponent);
                }
            }
        }
        return activeUnits.ToArray();
    }
}



