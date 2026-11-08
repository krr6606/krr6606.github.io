using UnityEngine;
using TMPro;
public class InGameEconomyManager : MonoBehaviour
{
    public static InGameEconomyManager Instance { get; private set; }
    public int gold { get; private set; } = 0;
    private float goldBuffer = 0;
    public int MaxGold { get; private set; } = 1000;
    public float goldPerSec { get; private set; } = 2f;
    public int maxEconomyLevel { get; private set; } = 1;
    public int maxLevel { get; private set; } = 10;
    public int DefaultIncreaseAmountMaxGold { get; private set; } = 50;
    public float DefaultIncreaseAmountGoldPerSec { get; private set; } = 1f;

    public int DefaultEconomyUpgradeCost = 40;
    public TMP_Text goldText;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        IncreaseGold(goldPerSec * Time.deltaTime * GameDataManager.Instance.userGameDataSO.levelDataList[(int)GameLevelDataName.goldPerSecLevel].level + ((maxEconomyLevel - 1) * DefaultIncreaseAmountGoldPerSec * Time.deltaTime ));
        UpdateGoldUI();
    }
    public void UpgradeEconomy()
    {
        maxEconomyLevel++;
        MaxGold += DefaultIncreaseAmountMaxGold 
            + (GameDataManager.Instance.userGameDataSO.levelDataList[(int)GameLevelDataName.goldPerSecLevel].level - 1)* DefaultIncreaseAmountMaxGold
            + (int)((maxEconomyLevel - 1) * DefaultIncreaseAmountMaxGold * 0.5f * GameDataManager.Instance.userGameDataSO.levelDataList[(int)GameLevelDataName.maxGoldLevel].level);
    }
    public void IncreaseGold(float amount)
    {
        
        goldBuffer += amount;
        if(goldBuffer > 1)
        {
            int goldToAdd = Mathf.FloorToInt(goldBuffer);
            goldBuffer -= goldToAdd;
            gold += goldToAdd;
        }

        if (gold > MaxGold)
        {
            gold = MaxGold;
        }
 
    }
    private void UpdateGoldUI()
    {
        goldText.text = $"{gold} / {MaxGold}"; 
    }

    public void SpendGold(int amount)
    {

            gold -= amount;
            UpdateGoldUI();

    }
    public void AddGold(int amount)
    {
        gold += amount;
        if (gold > MaxGold)
        {
            gold = MaxGold;
        }
        UpdateGoldUI();
    }
}
