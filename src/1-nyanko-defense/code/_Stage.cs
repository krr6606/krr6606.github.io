using System;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Stage", menuName = "ScriptableObjects/Stage", order = 1)]
public class _Stage : ScriptableObject
{
    public StageName stageName;
    public bool IsCleared;

    public int DefaultEXPReward;

    public int BossTriggerHealthPercentage;
    public EnemyName BossName;
    public List<StageWaveData> waves;

}



[Serializable]
public class StageWaveData
{
    [field: SerializeField] public EnemyName EnemyName {  get; private set; }
    [field: SerializeField] public float MinSpawnInterval { get; private set; }
    [field: SerializeField] public bool IsRandomTime { get; private set; }
    [field: SerializeField] public float MaxSpawnInterval { get; private set; }
    [field: SerializeField] public int SpawnCount { get; private set; }
    [field: SerializeField] public int MaxLoopCount { get; private set; }
    [field: SerializeField] public int NextWaveIndex {  get; private set; }

}
