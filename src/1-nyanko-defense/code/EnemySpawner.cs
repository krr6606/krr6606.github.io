using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public Transform PoolParnt;
    private GameObject[] isSpawned; // 각 y축 위치에 적이 이미 스폰되었는지 여부를 나타내는 배열
    [field: SerializeField] private List<Unit> enemyList = new List<Unit>();

    public List<ComponentPool> enemyPoolList = new List<ComponentPool>();
    _Stage stage;
    List<StageWaveData> waveList;
    private float SpawnTime;
    StageManager stageManager;
    private float CurrentSpawnTime;

    public int MaxSpawnCount = 50;
    private int currentWaveIndex;
    public bool isSpawnStopped = false;
    public float SpawnFrameDelay = 0.2f;

    private WaitForSeconds WaitSpawnFrameDelay;
    bool isBossSpawned = false;
    int BossTriggerHealthPercentage;
    private void Awake()
    {
        WaitSpawnFrameDelay = new WaitForSeconds(SpawnFrameDelay);
    }
    private void Start()
    {
        isSpawned = new GameObject[MaxSpawnCount];
        StageName currentStageName = StageManager.Instance.CurrentStageName;

        stage = StageDataManager.Instance.GetStageData(currentStageName);

        waveList = stage.waves;

        for (int i = 0; i < enemyList.Count; i++)
        {
            enemyPoolList.Add(new ComponentPool(enemyList[i].gameObject, PoolParnt));
        }
        stageManager = StageManager.Instance;
        BossTriggerHealthPercentage = StageDataManager.Instance.GetStageData(stageManager.CurrentStageName).BossTriggerHealthPercentage;
        StartCoroutine(WaveCo());
    }

    public Unit[] GetAllEnemies()
    {
        List<Unit> allEnemies = new List<Unit>();
        foreach (var pool in enemyPoolList)
        {
            allEnemies.AddRange(pool.GetAllActiveUnit());
        }
        return allEnemies.ToArray();
    }
    private IEnumerator WaveCo()
    {
        SpawnTime = waveList[currentWaveIndex].IsRandomTime ?
            Random.Range(waveList[currentWaveIndex].MinSpawnInterval, waveList[currentWaveIndex].MaxSpawnInterval)
            : waveList[currentWaveIndex].MinSpawnInterval;
        CurrentSpawnTime = SpawnTime;
        while (true)
        {
            if (stageManager.IsStageEnd())
            {
                yield break;
            }
            int activeCount = 0;
            for (int i = 0; i < isSpawned.Length; i++)
            {
                if (isSpawned[i] == null)
                {
                    continue;
                }
                if (isSpawned[i].activeSelf)
                {
                    activeCount++;
                }
            }
            isSpawnStopped = activeCount >= MaxSpawnCount;
            Debug.Log($"현재 활성화된 적 수: {activeCount}, 최대 스폰 수: {MaxSpawnCount}, 스폰 중지 여부: {isSpawnStopped}");
            // 보스 스폰 조건 확인
            if (BossTriggerHealthPercentage > 0 && isBossSpawned == false)
            {
                int currentHealthPercentage = Mathf.RoundToInt((float)stageManager.EnemyTower.CurrentHealth / stageManager.EnemyTower.MaxHealth * 100);
                if (currentHealthPercentage <= BossTriggerHealthPercentage) // 현재 체력 비율이 보스 스폰 조건 이하인 경우
                {
                    isBossSpawned = true;
                    EnemyName bossName = stage.BossName;
                    if (bossName == EnemyName.None)
                    {
                        Debug.LogError($"스테이지 {stage.name}의 BossName이 None으로 설정되어 있습니다. 올바른 값을 설정해주세요.");
                    }
                    else
                    {
                        Unit[] Cats = StageManager.Instance.catSpawner.GetAllCats();
                        for (int i = 0; i < Cats.Length; i++)
                        {
                            Cats[i].HitBack();
                        }
                        Hero hero = StageManager.Instance.hero;

                        hero.HitBack();
                        Unit boss = Spawn(bossName); // 보스 스폰
                        boss.useData.DefaultHealth *= 2; // 보스 체력 2배 증가
                        boss.CurrentHealth = boss.useData.DefaultHealth; // 현재 체력도 최대 체력으로 설정
                        boss.useData.DefaultDamage *= 1.1f; // 보스 공격력 1.1배 증가
                        StringSoundManager.Instance.PlaySFX("Snd-보스 등장");
                        StringSoundManager.Instance.PlayBGM("Bgm-전투-5-보스");
                    }
                }
            }
            // 웨이브 스폰이 중지되었는지 확인
            if (isSpawnStopped)
            {
                yield return null;
                continue;
            }
            if (CurrentSpawnTime > 0)
            {
                CurrentSpawnTime -= Time.deltaTime;

            }
            else 
            {
                Debug.Log($"웨이브 {currentWaveIndex} 스폰 시작: {waveList[currentWaveIndex].EnemyName}, 스폰 수: {waveList[currentWaveIndex].SpawnCount}");

                for (int i = 0; i < waveList[currentWaveIndex].SpawnCount; i++)
                {
                    Spawn(waveList[currentWaveIndex].EnemyName);
                    Debug.Log($"웨이브 {currentWaveIndex} 스폰 중: {waveList[currentWaveIndex].EnemyName}, 스폰 수: {i + 1}/{waveList[currentWaveIndex].SpawnCount}");
                    yield return WaitSpawnFrameDelay;
                }
                currentWaveIndex = waveList[currentWaveIndex].NextWaveIndex;
                SpawnTime = waveList[currentWaveIndex].IsRandomTime
                            ? Random.Range(waveList[currentWaveIndex].MinSpawnInterval, waveList[currentWaveIndex].MaxSpawnInterval)
                            : waveList[currentWaveIndex].MinSpawnInterval;
                CurrentSpawnTime = SpawnTime;
                if (currentWaveIndex < 0)
                {
                    Debug.LogError($"현재 웨이브 인덱스 {currentWaveIndex}의 NextWaveIndex 값이 0보다 작습니다.");
                    yield break;
                }
            }

            yield return null;

        }
    }

    Unit Spawn(EnemyName enemyName)
    {
        GameObject enemy = null;
        enemy = enemyPoolList[(int)enemyName - 1].Get();
        if (enemy == null)
        {
            return null;
        }
        
        SetPositionY(enemy);
        return enemy.GetComponent<Unit>();
    }
    private void SetPositionY(GameObject enemy)
    {
        for (int i = 0; i < isSpawned.Length; i++)
        {
            if (isSpawned[i] == null)
            {
                enemy.transform.position = new Vector3(transform.position.x, transform.position.y - i * 0.025f, transform.position.z);
                isSpawned[i] = enemy;
                enemy.GetComponent<SpriteRenderer>().sortingOrder = i;
                break;
            }
        }
    }
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}

