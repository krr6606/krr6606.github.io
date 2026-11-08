using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class SlotData : MonoBehaviour
{
    public RectTransform rectTransform { get; private set; }
    [field: SerializeField] public CatName catName { get; set; }
    [field: SerializeField] public GameLevelDataName levelName { get; set; }
    public int ResultSlotIndex;
    public Image image;
    public TMP_Text NameText;
    public TMP_Text LevelText;
    public TMP_Text upgradeEXP;

    public TMP_Text CostText;
    GameDataManager gameDataManager;

    public void Awake()
    {
        gameDataManager = GameDataManager.Instance;
        rectTransform = GetComponent<RectTransform>();
    }
    public void Start()
    {
        if (levelName != GameLevelDataName.none)
        {
            SetUserDataUI(levelName);
        }
    }
    public void SetNormalSlotData(CatName catName)
    {
        this.catName = catName;
        image.gameObject.SetActive(true);
        image.sprite = CatSlotSpriteDataManager.Instance.GetImageOrNull(catName);
        NameText.text = gameDataManager.CatSOArray[(int)catName].unitdata.Name;
        int level = gameDataManager.CatSOArray[(int)catName].unitdata.Level;
        if (level == 10)
        {
            upgradeEXP.text = "Max";
        }
        else
        {

            LevelText.text = gameDataManager.CatSOArray[(int)catName].unitdata.Level.ToString();
        }
        upgradeEXP.text = gameDataManager.CatSOArray[(int)catName].unitdata.UpgradeCost_1_10[gameDataManager.CatSOArray[(int)catName].unitdata.Level - 1].ToString();
    }
    public void SetResultSlotData(CatName catName)
    {
        this.catName = catName;
        image.gameObject.SetActive(true);
        image.sprite = CatSlotSpriteDataManager.Instance.GetCatSpriteOrNull(catName);

        if (CostText != null)
        {
            CostText.text = gameDataManager.CatSOArray[(int)catName].unitdata.Cost.ToString();
        }
        if (catName == CatName.None)
        {
            image.gameObject.SetActive(false);
        }
    }
    public void SetPosition(Vector2 inputPos)
    {
        rectTransform.anchoredPosition = inputPos;
    }
    public void UserDataLevelUp(GameLevelDataName levelName)
    {
        gameDataManager.userGameDataSO.LevelUp(levelName);
        SetUserDataUI(levelName);


    }
    public void SetUserDataUI(GameLevelDataName levelName)
    {

        if (gameDataManager.userGameDataSO.levelDataList[(int)levelName].level >= gameDataManager.userGameDataSO.LevelUpCost_1_10.Length)
        {
            LevelText.text = "Max";
            upgradeEXP.text = "";
            return;
        }
        LevelText.text = gameDataManager.userGameDataSO.levelDataList[(int)levelName].level.ToString();
        int index = gameDataManager.userGameDataSO.levelDataList[(int)levelName].level - 1;
        upgradeEXP.text = gameDataManager.userGameDataSO.LevelUpCost_1_10[index].ToString();
    }
}