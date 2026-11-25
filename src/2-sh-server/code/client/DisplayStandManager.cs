using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using TeamSH;
using UnityEngine;
using UnityEngine.Serialization;
using static TeamSH.Protocol;

public class DisplayStandManager : MonoBehaviour
{
    public static DisplayStandManager Instance { get; private set; }
    
    public Dictionary<CategoryType, AreaNum> CategoryDic = new Dictionary<CategoryType, AreaNum>();
    public CategoryType[] currentCategoryTypes; // UI에 CategoryType에 맞게 정렬하기 위해 임시로 빼둠
    
    public List<AreaData> Areas = new();

    public GameObject ItemBox; //아이템 담을 박스 프리팹 원본
    public Transform ItemContainer { get; private set; } // 아이템을 실제로 담아 둘 컨테이너 좌표

    public int AllStandCount = 0;
    public bool IsReady { get; private set; } = false;
    public bool IsFinishSetItem { get; private set; } = false;

    #region 기본 설정

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            GameObject container = new GameObject("MartItemContainer");
            ItemContainer = container.transform;
            ItemContainer.SetParent(null); // 씬 루트에 위치
            ItemContainer.localScale = Vector3.one;
            ItemContainer.rotation = Quaternion.identity;
            Init();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Init()
    {
        AreaData area1 = new AreaData();
        AreaData area2 = new AreaData();
        AreaData area3 = new AreaData();
        area1.AreaNum = AreaNum.Area1;
        area2.AreaNum = AreaNum.Area2;
        area3.AreaNum = AreaNum.Area3;

        Areas.Add(area1);
        Areas.Add(area2);
        Areas.Add(area3);
       List<DisplayStand> allStands = FindObjectsByType<DisplayStand>(FindObjectsSortMode.None).ToList();
        AllStandCount = allStands.Count;
        allStands.Sort((a, b) => a.name.CompareTo(b.name));
        for (int i = 0; i < allStands.Count; i++)
        {
            RegisterDisplayStand(allStands[i]);
        }
        await UniTask.WaitUntil(() => DataManager.Instance.IsReady);
        
        IsReady = true;
    }
    public void RegisterDisplayStand(DisplayStand displayStand)
    {
 

        AreaData area = Areas[(int)displayStand.areaNum];
        if (area.DisplayStands.Contains(displayStand) == false)
        {
            area.DisplayStands.Add(displayStand);
        }

    }

    #endregion
    
    // 가까운 매대 아이템 찾기
    public DisplayStand CheckWhenStend(Vector3 target)
    {
        for (int i = 0; i < Areas.Count; i++)
        {
            for (int j = 0; j < Areas[i].DisplayStands.Count; j++)
            {
                if (Areas[i].DisplayStands[j].IsStay(target))
                {
                    return Areas[i].DisplayStands[j];
                }
            }
        }
        return null;
    }

    /// 전체 매대를 순회하며 itemId를 가진 매대와 슬롯 인덱스찾기
    public bool TryFindStandByItemId(int itemId, out DisplayStand stand, out int index)
    {
        for (int i = 0; i < Areas.Count; i++)
        {
            for (int j = 0; j < Areas[i].DisplayStands.Count; j++)
            {
                DisplayStand s = Areas[i].DisplayStands[j];
                int idx = s.GetItemIndexById(itemId);
                if (idx >= 0)
                {
                    stand = s;
                    index = idx;
                    return true;
                }
            }
        }
        stand = null;
        index = -1;
        return false;
    }
    
    public void UnregisterDisplayStand(DisplayStand displayStand)
    {

        AreaData area = Areas[(int)displayStand.areaNum];
        if (area.DisplayStands.Contains(displayStand))
        {
            area.DisplayStands.Remove(displayStand);
        }
    }
    /// <summary>
    /// 포지션이 어떤 스탠드 안에 있는지 모든 스탠드를 검사하고, 있다면 해당 스탠드를 반환합니다. 없다면 null을 반환합니다.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>

    #region 매대 배치 내부 설정

    public DisplayStand CheckWhenStend(Vector3 origin ,Vector3 target)
    {
        for (int i = 0; i < Areas.Count; i++)
        {
            for (int j = 0; j < Areas[i].DisplayStands.Count; j++)
            {
                if (Areas[i].DisplayStands[j].IsStay(target))
                {
                    return Areas[i].DisplayStands[j];
                }
            }
        }
        return null;
    }
    
    /// <summary>
    ///  원점 (origin)에서 목표(target)까지의 레이를 쏴서, 어떤 스탠드와 충돌하는지 검사합니다. 충돌한다면 해당 스탠드를 반환합니다. 없다면 null을 반환합니다.
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public DisplayStand CheckWhenStandRay(Vector3 origin, Vector3 target)
    {
        for (int i = 0; i < Areas.Count; i++)
        {
            for (int j = 0; j < Areas[i].DisplayStands.Count; j++)
            {
                if (Areas[i].DisplayStands[j].RayCheck(origin, target))
                {
                    return Areas[i].DisplayStands[j];
                }
            }
        }
        return null;
    }
    public void ClearAllDisplayStand()
    {
        for (int i = 0; i < Areas.Count; i++)
        {
            Areas[i].DisplayStands.Clear();
        }
    }
    /// <summary>
    /// 없다면 null을 반환합니다.
    /// </summary>
    /// <param name="categoryType"></param>
    /// <returns></returns>
    public List<DisplayStand> GetDisplayStandByCategory(CategoryType categoryType)
    {
        if (CategoryDic.ContainsKey(categoryType))
        {
            return Areas[(int)CategoryDic[categoryType]].DisplayStands;
        }

        return null;
    }
    // 서버에서 랜덤으로 중복되지 않게 카테고리를 주입해줘야 함
    public void SetCategoryAllArea(CategoryType[] categoryTypes)
    {
        if (categoryTypes.Length != Areas.Count)
        {
            Debug.LogError
                ($"[DisplayStandManager] : SetCategoryAllArea failed. " +
                 $"categoryTypes length {categoryTypes.Length} does not match AllArea count {Areas.Count}.");
            return;
        }

        CategoryDic.Clear(); //딕셔너리 초기화
        
        for (int i = 0; i < categoryTypes.Length; i++)
        {
            CategoryDic[categoryTypes[i]] = (AreaNum)i;
        }
    }


    
    public async UniTask SetItemIdBufferForArea(CategoryItemArray[] categoryItemArrays)
    {
        IsFinishSetItem = false; //처음들어왔을때 false로 시작
        
        currentCategoryTypes = new CategoryType[categoryItemArrays.Length];
        for(int i = 0; i < categoryItemArrays.Length; i++)
        {
            currentCategoryTypes[i] = categoryItemArrays[i].Category;
        }
        
        SetCategoryAllArea(currentCategoryTypes);
        
        for(int i = 0; i < currentCategoryTypes.Length; i++)
        {
            int areDataIdx = (int)CategoryDic[currentCategoryTypes[i]];
            Debug.Log($"[DisplayStandManager] : Setting item ID buffer for area {areDataIdx} with category {currentCategoryTypes[i]}.");
            
            List<int> intIds = new List<int>();
            for(int j = 0; j < categoryItemArrays[i].ItemIds.Length; j++)
            {
                int itemId = int.Parse(categoryItemArrays[i].ItemIds[j]);
                Debug.Log($"[DisplayStandManager] : Adding item ID {itemId} to buffer for area {areDataIdx}.");
                intIds.Add(itemId);
            }
            
            AreaData area = Areas[areDataIdx];
            area.AddItems(intIds);
        }
        
        int count = 0;
        foreach (var CategoryType in currentCategoryTypes)
        {
            if (count >= Areas.Count)
            {
                Debug.LogError(
                    $"[DisplayStandManager] : SetItemIdBufferForArea failed. categoryItemArrays length {categoryItemArrays.Length} exceeds Areas count {Areas.Count}.");
                return;
            }

            AreaData area = Areas[(int)CategoryDic[CategoryType]];

            for (int j = 0; j < area.DisplayStands.Count; j++)
            {
                SetCategoryItemInStand(area.DisplayStands[j]);
            }
            count++;
        }
        
        IsFinishSetItem = true;
    }


    private void SetCategoryItemInStand(DisplayStand displayStand)
    {
        AreaData area = Areas[(int)displayStand.areaNum];

        // 3은 아이템 개수 하드코딩. 추후 수정
        for (int i = 0; i < DisplayStand.ItemMaxCount; i++)
        {
            if (area.ItemBuffers.Count <= 0)
            {
                Debug.LogWarning($"[DisplayStandManager] : No more items in buffer for area {area.AreaNum}. Cannot set item in stand {displayStand.name}.");
                break;
            }
            Debug.Log($"[DisplayStandManager] : Setting item {area.ItemBuffers[0].ItemId} in stand {displayStand.name} for area {area.AreaNum}.");
            displayStand.SetItem(area.ItemBuffers[0]);
            area.ItemBuffers.RemoveAt(0);
        }
    }

    private List<int> GetIdBufferByArea(AreaNum areaNum)
    {
        AreaData area = Areas[(int)areaNum];
        return area.ItemIdBuffer;
        // switch (areaNum)
        // {
        //     case AreaNum.Area1:
        //         return Area1ItemIdBuffer;
        //     case AreaNum.Area2:
        //         return Area2ItemIdBuffer;
        //     case AreaNum.Area3:
        //         return Area3ItemIdBuffer;
        //     default:
        //         return null;
        // }
    }
    private List<ItemDef> GetItemBufferByArea(AreaNum areaNum)
    {
        AreaData area = Areas[(int)areaNum];
        return area.ItemBuffers;
        // switch (areaNum)
        // {
        //     case AreaNum.Area1:
        //         return Area1ItemBuffer;
        //     case AreaNum.Area2:
        //         return Area2ItemBuffer;
        //     case AreaNum.Area3:
        //         return Area3ItemBuffer;
        //     default:
        //         return null;
        // }
    }

    #endregion
}
