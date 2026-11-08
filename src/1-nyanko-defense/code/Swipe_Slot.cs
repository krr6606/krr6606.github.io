using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Swipe_Slot : MonoBehaviour
{
    public Scrollbar scrollbar;
    public List<SlotData> slotDataList;
    public List<StageSlot> stageSlotList;
    public int currentIndex = 0;
    float[] pos;
    float distance;
    bool isDragging = false;
    public int targetIndex { get; private set; }

    List<SlotData> CatDatas;

    public GameObject slotPrefab;
    public InvenType invenType;

    GameDataManager gameDataManager;
    private void Awake()
    {

        CatDatas = new List<SlotData>();
        for (int i = 0; i < transform.childCount; i++)
        {
            SlotData slotData = transform.GetChild(i).GetComponent<SlotData>();
            if (slotData != null)
            {
                slotDataList.Add(slotData);

            }
            else
            {
                StageSlot stageSlot = transform.GetChild(i).GetComponent<StageSlot>();
                stageSlotList.Add(stageSlot);
            }
        }

    }
    void Start()
    {

        gameDataManager = GameDataManager.Instance;
        int maxclearedStage = (int)gameDataManager.userGameDataSO.maxclearedStage;
        if (invenType == InvenType.StageInven)
        {
            for (int i = 0; i < stageSlotList.Count; i++)
            {
                if ((int)stageSlotList[i].StageName > maxclearedStage)
                {
                    stageSlotList[i].gameObject.SetActive(false);
                }
                else
                {
                    stageSlotList[i].gameObject.SetActive(true);
                }

            }
            if ((int)maxclearedStage < stageSlotList.Count)
            {
                stageSlotList[maxclearedStage].gameObject.SetActive(true);
            }
        }
        Init();
        for(int i = 0; i < slotDataList.Count; i++)
        {
            Debug.Log($"슬롯 고양이 enum: {slotDataList[i].catName},슬롯 레벨 enum: {slotDataList[i].levelName}");
        }
        int StageClearMaxCount = Mathf.Min((int)gameDataManager.userGameDataSO.maxclearedStage + 1, stageSlotList.Count);
        pos = (invenType == InvenType.StageInven)
            ? new float[StageClearMaxCount]
            : new float[transform.childCount];
        distance = 1f / (pos.Length - 1f);

        for (int i = 0; i < pos.Length; i++)
        {
            pos[i] = distance * i;
        }




    }

    private void Update()
    {
        if (invenType == InvenType.None||invenType == InvenType.ResultInven) return;
        if (Mouse.current.leftButton.isPressed)
        {
            isDragging = true;

        }
        else
        {

            // 드래그를 막 놓은 순간 → 가장 가까운 인덱스 1회 계산
            if (isDragging)
            {
                isDragging = false;
                SlotChangeSound();
                targetIndex = GetNearestIndex(scrollbar.value);
            }
            // 매 프레임 target 위치로 Lerp
            scrollbar.value = Mathf.Lerp(scrollbar.value, pos[targetIndex], 0.1f);
        }

        // 스케일은 targetIndex 기준으로만 처리
        for (int i = 0; i < pos.Length; i++)
        {
            Vector2 targetScale = (i == targetIndex)
                ? new Vector2(1f, 1f)
                : new Vector2(0.8f, 0.8f);

            transform.GetChild(i).localScale =
                Vector2.Lerp(transform.GetChild(i).localScale, targetScale, 0.1f);
        }
        currentIndex = targetIndex;

    }

    // 현재 value에서 가장 가까운 pos 인덱스 반환
    int GetNearestIndex(float value)
    {
        int nearest = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < pos.Length; i++)
        {
            float dist = Mathf.Abs(value - pos[i]);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }
        return nearest;
    }
    public float GetNearestValue(int index)
    {
        if (index >= 0 && index < pos.Length)
        {
            return pos[index];
        }
        return -1f;
    }
    public CatName GetCatName(int index)
    {
        if (index >= 0 && index < CatDatas.Count)
        {
            return CatDatas[index].catName;
        }
        return CatName.None;
    }
    public void SetCatName(int index, CatName catName)
    {
        if (index >= 0 && index < CatDatas.Count)
        {
            CatDatas[index].catName = catName;
        }
    }
    private void Init()
    {
        switch (invenType)
        {
            case InvenType.None:

                break;
            case InvenType.PickInven:
                int _count = gameDataManager.userGameDataSO.UnLockList.Count;
                for (int i = 0; i < _count; i++)
                {
                    if (isInList(gameDataManager.userGameDataSO.UnLockList[i]) || gameDataManager.userGameDataSO.UnLockList[i] == CatName.None)
                    {
                        continue;
                    }
                    GameObject slotObj = Instantiate(slotPrefab, transform);
                    SlotData slotData = slotObj.GetComponent<SlotData>();
                    CatName catName = gameDataManager.userGameDataSO.UnLockList[i];
                    slotData.SetNormalSlotData(catName);
                    CatDatas.Add(slotData);


                    InvenManager.Instance.Register(this);
                }
                break;
            case InvenType.ResultInven:
                CatDatas = new List<SlotData>(GetComponentsInChildren<SlotData>());
                for (int i = 0; i < gameDataManager.userGameDataSO.CatSlotArray.Length; i++)
                {
                    CatName catName = gameDataManager.userGameDataSO.CatSlotArray[i];
                    if (catName != CatName.None)
                    {
                        CatDatas[i].SetResultSlotData(catName);
                        Debug.Log($"ResultInven Slot {i}: {CatDatas[i].catName}");
                    }
                    InvenManager.Instance.Register(this);
                }
                break;
            case InvenType.PowerUpInven:
                int __count = gameDataManager.userGameDataSO.UnLockList.Count;
                for (int i = 0; i < __count; i++)
                {
                    if (gameDataManager.userGameDataSO.UnLockList[i] == CatName.None)
                    {
                        continue;
                    }
                    GameObject slotObj = Instantiate(slotPrefab, transform);
                    SlotData slotData = slotObj.GetComponent<SlotData>();
                    CatName catName = gameDataManager.userGameDataSO.UnLockList[i];
                    slotData.SetNormalSlotData(catName);
                    CatDatas.Add(slotData);


                }
                Debug.Log($"PowerUpInven CatDatas Count: {CatDatas.Count}");
                for (int j = CatDatas.Count - 1; j >= 0; j--)
                {
                    CatDatas[j].gameObject.transform.SetSiblingIndex(0);
                    slotDataList.Insert(0, CatDatas[j]);
                    Debug.Log($"PowerUpInven Slot {j}: {CatDatas[j].catName}");
                }
                break;
            default:
                break;
        }

    }
    public void SlotChangeSound()
    {
        StringSoundManager.Instance.PlaySFX("Snd-슬롯 변경");
    }
    public bool isInList(CatName catName)
    {
        for (int i = 0; i < CatDatas.Count; i++)
        {
            if (CatDatas[i].catName == catName)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsIn(SlotData slot)
    {
        if (slot == null) return false;

        for (int i = 0; i < CatDatas.Count; i++)
        {
            if (CatDatas[i] == slot)
            {
                return true;
            }
        }


        return false;
    }
}