using UnityEngine;

public class StageMapControll : MonoBehaviour
{
     Camera mainCamera;
    [SerializeField] private GameObject[] MapPoints;
    Swipe_Slot swipe_Slot;
    int currentStageIndex;
    private void Awake()
    {
        swipe_Slot = FindAnyObjectByType<Swipe_Slot>();
    }
    private void Start()
    {
        mainCamera = Camera.main;
        int maxclearedStage = (int)GameDataManager.Instance.userGameDataSO.maxclearedStage;
        for (int i = 0; i < MapPoints.Length; i++)
        {
            if (i <= maxclearedStage)
            {
                MapPoints[i].SetActive(true);
            }
            else
            {
                MapPoints[i].SetActive(false);
            }
        }

        currentStageIndex = maxclearedStage;
        float nearestValue = swipe_Slot.GetNearestValue(currentStageIndex);

        if (nearestValue == -1f) return;

        swipe_Slot.scrollbar.value = nearestValue;

    }
    private void Update()
    {
        if (currentStageIndex != swipe_Slot.currentIndex)
        {
            currentStageIndex = swipe_Slot.currentIndex;

        }

        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, MapPoints[currentStageIndex].transform.position + new Vector3(0, 0, -10), Time.deltaTime * 5f);

    }

}
