using UnityEngine;

public class Unit : BaseUnit
{
    public CatName catName;
    public EnemyName enemyName;

    public Walk WalkState { get; private set; }
    public Idle IdleState { get; private set; }
    public Attack AttackState { get; private set; }
    public HitBack HitBackState { get; private set; }
    public Die DieState { get; private set; }

    override protected void Awake()
    {
        base.Awake();

        WalkState = new Walk(stateMachine, "IsWalk", this);
        IdleState = new Idle(stateMachine, "IsIdle", this);
        AttackState = new Attack(stateMachine, "IsAttack", this);
        HitBackState = new HitBack(stateMachine, "IsHitBack", this);
        DieState = new Die(stateMachine, "IsDie", this);
    }
    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(WalkState);


    }
    private void OnEnable()
    {
        stateMachine.Initialize(WalkState);
        CurrentHealth = useData.DefaultHealth;
        collider2d.enabled = true;
    }
    protected override void Attack()
    {

        float dir = useData.isCat ? -1f : 1f;
        if (useData.IsOneTarget)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.right * dir * (useData.AttackRangeStart * multiplier), Vector2.right * dir, useData.AttackRangeEnd * multiplier, TargetLayer);
            if (hit.collider == null) return;
            IDamagable damagable = hit.collider.GetComponent<IDamagable>();
            if (damagable == null) return;
            Debug.Log("공격 대상: " + hit.collider.name + ", 현재 타겟레이어: " + LayerMask.LayerToName(hit.collider.gameObject.layer));
            damagable.TakeDamage(useData.DefaultDamage, ElementalType.normal, transform);
        }
        else
        {
            RaycastHit2D[] hitList = Physics2D.RaycastAll(transform.position + Vector3.right * dir * (useData.AttackRangeStart * multiplier), Vector2.right * dir, useData.AttackRangeEnd * multiplier, TargetLayer);
            if (hitList.Length == 0) return;
            foreach (RaycastHit2D hit in hitList)
            {
                IDamagable damagable = hit.collider.GetComponent<IDamagable>();
                if (damagable == null) continue;
                damagable.TakeDamage(useData.DefaultDamage, ElementalType.normal, transform);
            }
        }
        animatorHit.gameObject.SetActive(true);
    }
    public void Kill()
    {
        CurrentHealth = 0;
        stateMachine.ChangeState(DieState);
    }
    public void HitBack()
    {
        stateMachine.ChangeState(HitBackState);
    }
    public override void TakeDamage(float damage, ElementalType damageType, Transform attacker)
    {
        if (IsInvincibility) return;
        int RandomSound = Random.Range(0, 2);
        switch (RandomSound)
        {
            case 0:
                StringSoundManager.Instance.PlaySFX("Snd-공격 성공-1");
                break;
            case 1:
                StringSoundManager.Instance.PlaySFX("Snd-공격 성공-2");
                break;
        }

        CurrentHealth -= damage;
        if (CurrentHealth <= 0)
        {
            stateMachine.ChangeState(HitBackState);
            return;
        }
        if (HitBackCount == useData.HitBackMaxCount) return;

        float hitBackPoint = useData.DefaultHealth * ((useData.HitBackMaxCount - HitBackCount) / (float)useData.HitBackMaxCount);
        hitBackPoint = Mathf.RoundToInt(hitBackPoint);
        Debug.Log($"체력: {CurrentHealth}, 히트백 포인트: {hitBackPoint}, 히트백 카운트: {HitBackCount}, 히트백 최대 카운트: {useData.HitBackMaxCount}");
        if (CurrentHealth <= hitBackPoint)
        {
            ++HitBackCount;
            stateMachine.ChangeState(HitBackState);
        }
    }
}
