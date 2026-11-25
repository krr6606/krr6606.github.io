using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TeamSH;
using UnityEngine;

/// <summary>
/// 임시 카테고리. 나중에 데이터 테이블 쪽에서 정의될 예정
/// </summary>
public enum AreaNum
{
    Area1,
    Area2,
    Area3,
}
public class DisplayStand : MonoBehaviour
{
    [Header("매대 범위 설정")]
    public float width;
    public float height;
    public float depth;
    float widthScale = 1f;
    float heightScale = 1f;
    float depthScale = 1f;
    
    private LineRenderer ViewGrid;
   [field: SerializeField] public bool ShowGrid { get; set; } = false;
    
    private MeshFilter meshFilter;
    private GridCell grid;
    public GridCell Grid => grid ??= new GridCell(0, 0, transform.position);

    [Header("매대 아이템 설정")]
    
    [field: SerializeField] public AreaNum areaNum { get; private set; } = AreaNum.Area1;
    public CategoryType CategoryType { get; set; } = CategoryType.None;

    // 아이템 여러 개 기억하기 위해 일단 임시 코드
    public List<ItemDef> Items { get; private set; } = new();
    public List<MartItem> MartItems { get; private set; } = new();
    
    // 아이템 3등분 위치
    private readonly List<Vector3> itemPositions = new();
    public const int ItemMaxCount = 3;

    #region 진열대 기본 설정

    void Awake()
    {
        //parent = transform.parent;
        ViewGrid = GetComponent<LineRenderer>();
        InitSize();
        SetItemPosition();
    }

    void Start()
    {
        Debug.Log($"[DisplayStand] AreaNumber : {areaNum} - {gameObject.name}");
        Register();
    }
    void OnDisable()
    {
        DisplayStandManager.Instance?.UnregisterDisplayStand(this);
    }
    private void Register()
    {
        DisplayStandManager.Instance.RegisterDisplayStand(this);
    }
    
    void InitSize()
    {
        meshFilter = GetComponent<MeshFilter>();
        width = meshFilter.mesh.bounds.size.x;
        height = meshFilter.mesh.bounds.size.y;
        depth = meshFilter.mesh.bounds.size.z;

        widthScale = transform.lossyScale.x;
        heightScale = transform.lossyScale.y;
        depthScale = transform.lossyScale.z;

        width *= widthScale;
        height *= heightScale;
        depth *= depthScale;

        InitGrid();
    }
    
    void InitGrid()
    {
        // 피봇을 mesh의 중심으로 옮기는 오프셋 계산 
        Vector3 centerOffset = meshFilter.mesh.bounds.center;
        centerOffset = new Vector3(centerOffset.x * widthScale, centerOffset.y * heightScale, centerOffset.z * depthScale);
        grid = new GridCell(width, depth, transform.position + centerOffset);
        SetViewGrid();
        Debug.Log($"[그리드 디버그 로직] {grid.GetCenterPosition()}");
    }
    
    private void SetViewGrid()
    {
        if (ViewGrid == null) return;
        if (ShowGrid == false)
        {
            ViewGrid.enabled = false;
            return;
        }
        else
        {
            ViewGrid.enabled = true;
        }


        ViewGrid.positionCount = 4;
        Vector3[] positions = new Vector3[4];
        for(int i = 0; i < 4; i++)
        {
            positions[i] = grid.GetGridCorner(i);
        }

        ViewGrid.SetPositions(positions);
    }
    
    /// <summary>
    /// DisplayStand의 width(x)를 n등분하고 아이템을 가져올 위치를 저장한다.
    /// </summary>
    public void SetItemPosition()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        float startPointX = meshFilter.mesh.bounds.min.x * widthScale;
        float dividedLength = width / ItemMaxCount;

        for (int i = 0; i < ItemMaxCount; i++)
        {
            float x = startPointX + dividedLength * i + dividedLength * 0.5f;
            float z = meshFilter.mesh.bounds.center.z * depthScale;
            
            Vector3 position = new Vector3(x, 0f, z);

            itemPositions.Add(position);
        }
    }

    #endregion
    
    
    # region 내부 설정(Set)
    /// <summary>
    /// AreaNum을 CategoryAreaManager에 연결짓는 함수
    /// </summary>
    /// <param name="item"></param>
    public void SetAreaNum(AreaNum areaNum)
    {
        this.areaNum = areaNum;
    }

    public void SetItem(ItemDef item)
    {
        Items.Add(item);
        CategoryType = item.CategoryType;
        // 아이템 프리펩이 로드되지 않았다면 리턴(현재는 프리펩이 없으므로 이 로직을 만듬) 
        if(item.PrefabKey == null) return;
        if (MartItems.Count >= itemPositions.Count) return;

        Vector3 itemPosition = itemPositions[MartItems.Count];
        
        MartItem martItem = Instantiate
        (item.PrefabKey, 
            transform.position + new Vector3(itemPosition.x, height + 1.5f, itemPosition.z),
            item.PrefabKey.transform.rotation);
        Debug.Log($"[DisplayStand] SetItem : {item?.ItemName} - {martItem?.name}");
        if (martItem == null) return;
        martItem.itemDef = item;
        //martItem?.transform.SetParent(transform); //매대에 자식으로 넣으니 스케일문제가 생김
        martItem.transform.SetParent(DisplayStandManager.Instance.ItemContainer, true);
        MartItems.Add(martItem);
    }
    
    # endregion

    #region 외부 연결 설정(Get)

    /// 이 매대 안에서 주어진 itemId를 가진 아이템의 슬롯 인덱스를 찾기.
    public int GetItemIndexById(int itemId)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] != null && Items[i].ItemId == itemId) return i;
        }
        return -1;
    }
    
    //  가장 가까운 활성화된 아이템 인덱스 가져오기
    public int GetNearestActiveItemIndex(Vector3 playerPosition)
    {
        int nearestIndex = -1;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < MartItems.Count; i++)
        {
            if (MartItems[i] == null) continue;
            if (!MartItems[i].gameObject.activeSelf) continue; // 이미 집힌 아이템은 후보 제외

            float distance = (playerPosition - MartItems[i].transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }
    
    public ItemDef GetNearestItem(Vector3 playerPosition)
    {
        int idx = GetNearestActiveItemIndex(playerPosition);
        return idx < 0 ? null : MartItems[idx]?.itemDef;
    }
    
    public MartItem RequestCreateBox(Vector3 spawnPos, int index) // 박스 생성 요청
    {
        GameObject newBox = Instantiate(DisplayStandManager.Instance.ItemBox, spawnPos, Quaternion.identity);
        newBox.AddComponent<MartItem>();
        MartItem box = newBox.GetComponent<MartItem>();
        box.SetItemInfo(this, Items[index], index); //여기 아이템 리스트중 하나 골라 보내기코드 필요
        box.KillAnimation();
        RequestActiveItem(index);
        return box;
    }
    
    public void RequestActiveItem(int index) // 매대의 물건 활성화/ 비 활성화 호출
    {
        if(MartItems[index].gameObject.activeSelf) MartItems[index].gameObject.SetActive(false);
        else MartItems[index].gameObject.SetActive(true);
    }

    #endregion
    
    public bool IsStay(Vector3 target)
    {
        return grid.IsStay(target);
    }
    public bool RayCheck(Vector3 origin, Vector3 target)
    {
        return grid.RayCheck(origin, target);
    }
}
