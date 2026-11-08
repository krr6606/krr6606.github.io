using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;
using static UnityEngine.UIElements.UxmlAttributeDescription;
namespace Study_Inventory
{


    public class InventoryInput : MonoBehaviour, IPointerUpHandler, IDragHandler, IPointerDownHandler
    {

        public ScrollRect scroll;
        [field: SerializeField] public SlotData cursorSlot { get; private set; }

        private GraphicRaycaster raycaster;
        private SlotData startSlot;    //드래그를 시작한 위치의 슬롯
        private SlotData endSlot;      //드래그가 종료된 위치의 슬롯

        private void Awake()
        {
            raycaster = GetComponent<GraphicRaycaster>();

        }
        private SlotData GetSlotOrNull(PointerEventData eventData)
        {
            List<RaycastResult> raycastResults = new List<RaycastResult>();
            //canvas에 붙어있는 GraphicRaycaster를 이용해서
            //마우스 위치에 있는 UI 요소들을 raycast하고
            //그 결과를 raycastResults에 저장한다.
            raycaster.Raycast(eventData, raycastResults);

            foreach (RaycastResult raycastResult in raycastResults)
            {
                if (raycastResult.gameObject.TryGetComponent<SlotData>(out SlotData slot))
                {
                    if (slot == cursorSlot) continue; //cursorSlot은 제외한다.
                    return slot;
                }
            }
            return null;
        }
        public void OnPointerDown(PointerEventData eventData)
        {

            startSlot = GetSlotOrNull(eventData);

            if (startSlot == null) return;  //검색된 슬롯이 없거나
            if (startSlot.catName == CatName.None) return;   //검색된 슬롯이 빈 슬롯인 경우
            if(InvenManager.Instance.GetInventoryOrNull(startSlot).invenType == InvenType.PickInven)
            {
                cursorSlot.gameObject.SetActive(true);
                cursorSlot.SetNormalSlotData(startSlot.catName);
                
            }
            else if(InvenManager.Instance.GetInventoryOrNull(startSlot).invenType == InvenType.ResultInven)
            {
                cursorSlot.gameObject.SetActive(true);
                cursorSlot.SetResultSlotData(startSlot.catName);
                startSlot.SetResultSlotData(CatName.None);
        
                GameDataManager.Instance.userGameDataSO.CatSlotArray[startSlot.ResultSlotIndex] = CatName.None;
            }
            scroll.enabled = false;
            cursorSlot.SetPosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {

            if (startSlot == null || cursorSlot.gameObject.activeSelf == false || cursorSlot.catName == CatName.None) return; //드래그 시작되지 않았음
            cursorSlot.SetPosition(eventData.position); //CursorSlot의 위치를 마우스 위치로 설정한다.
            Debug.Log($"드래그 중: {cursorSlot.catName}");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 3. 드래그가 종료되면 상황에 따라 알맞은 처리를 한다.
            //  3.1 종료된 위치의 Slot이 빈 Slot인 경우 : Cursor의 아이템을 Slot에 Set한다.
            //  3.2 종료된 위치의 Slot이 아이템이 존재하는 Slot인 경우 : Cursor의 아이템과 Slot의 아이템을 서로 교환한다.
            //  3.3 종료된 위치가 Slot이 아닌 경우 : Cursor의 아이템을 원래 Slot에 Set한다.

            if (startSlot == null) return;
            if(cursorSlot.catName == CatName.None) {Debug.Log("드래그 종료: cursorSlot이 빈 슬롯입니다."); return;}
            endSlot = GetSlotOrNull(eventData);
            Swipe_Slot startInven = InvenManager.Instance.GetInventoryOrNull(startSlot);
            Swipe_Slot endInven = InvenManager.Instance.GetInventoryOrNull(endSlot);
            if (endSlot == null)
            {
                
                if(startInven.invenType == InvenType.ResultInven)
                {
                    startSlot.SetResultSlotData(cursorSlot.catName);
                    GameDataManager.Instance.userGameDataSO.CatSlotArray[startSlot.ResultSlotIndex] = cursorSlot.catName;
   
                }

                ResetCursor();
                return; //종료된 위치가 Slot이 아닌 경우
            }
            else if(endInven.isInList(cursorSlot.catName))
            {
                if (startInven.invenType == InvenType.ResultInven)
                {
                    startSlot.SetResultSlotData(cursorSlot.catName);
                    GameDataManager.Instance.userGameDataSO.CatSlotArray[startSlot.ResultSlotIndex] = cursorSlot.catName;

                }

                ResetCursor();
                return; //종료된 위치의 인벤토리에 해당 고양이가 없는 경우
            }
            else if (endSlot.catName == CatName.None 
                && endInven != null 
                && endInven.invenType == InvenType.ResultInven)
            {

                endSlot.SetResultSlotData(cursorSlot.catName);
                GameDataManager.Instance.userGameDataSO.CatSlotArray[endSlot.ResultSlotIndex] = cursorSlot.catName;
            }
            else if(endSlot.catName != CatName.None 
                && endInven != null 
                && endInven.invenType == InvenType.ResultInven
                && startInven.invenType == InvenType.ResultInven)
            {

                CatName tempCatName = endSlot.catName;
                endSlot.SetResultSlotData(cursorSlot.catName);
                GameDataManager.Instance.userGameDataSO.CatSlotArray[endSlot.ResultSlotIndex] = cursorSlot.catName;
                startSlot.SetResultSlotData(tempCatName);
                GameDataManager.Instance.userGameDataSO.CatSlotArray[startSlot.ResultSlotIndex] = tempCatName;
            }
            else if(endSlot.catName != CatName.None
                && endInven != null
                && endInven.invenType == InvenType.ResultInven
                && startInven.invenType == InvenType.PickInven)
            {
                endSlot.SetResultSlotData(cursorSlot.catName);
                GameDataManager.Instance.userGameDataSO.CatSlotArray[endSlot.ResultSlotIndex] = cursorSlot.catName;
            }
            ResetCursor();

        }
        private void ResetCursor()
        {

            cursorSlot.SetResultSlotData(CatName.None);
            cursorSlot.gameObject.SetActive(false);
            startSlot = null;
            scroll.enabled = true;
        }


    }
}