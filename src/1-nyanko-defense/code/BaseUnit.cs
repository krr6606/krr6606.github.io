using UnityEngine;

public class BaseUnit : MonoBehaviour , IDamagable
{

    public EntitySO entitySOData;

    public Unitdata useData;

    protected StateMachin stateMachine;
    public float CurrentHealth { get;  set; }
    protected int HitBackCount;
    public bool IsInvincibility { get; set; }
    public LayerMask TargetLayer;

    public float originYPosition;

    protected SpriteRenderer spriteRenderer;

    public Animator animator;

    public Animator animatorHit;

    public bool isAttacking;
    public float multiplier = 0.005f;
    public Collider2D collider2d { get; protected set; }

    virtual protected void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        stateMachine = new StateMachin();
        collider2d = GetComponent<Collider2D>();
    }
    protected virtual void Start()
    {
        InitData();
        originYPosition = transform.position.y;
        CurrentHealth = useData.DefaultHealth;

    }

    protected virtual void Update()
    {
        stateMachine.UpdateActiveState();
    }
    public virtual void InitData()
    {
        float levelMultiplier = 0.2f; 
        if(useData.Level > 5)
        {
            levelMultiplier = 0.1f;
        }
        HitBackCount = 1;
        useData.Level = Mathf.Clamp(useData.Level, 1, 10);
        useData.DefaultHealth = entitySOData.unitdata.DefaultHealth + (entitySOData.unitdata.Level - 1) * entitySOData.unitdata.DefaultHealth * levelMultiplier;
        Debug.Log($"�⺻ ü�� : {useData.DefaultHealth}");
        useData.DefaultDamage = entitySOData.unitdata.DefaultDamage + (entitySOData.unitdata.Level - 1) * entitySOData.unitdata.DefaultDamage * levelMultiplier;
        useData.HitBackMaxCount = entitySOData.unitdata.HitBackMaxCount;
        useData.isCat = entitySOData.unitdata.isCat;
        useData.speed = entitySOData.unitdata.speed;
        useData.Range = entitySOData.unitdata.Range;
        useData.AttackRangeStart = entitySOData.unitdata.AttackRangeStart;
        useData.AttackRangeEnd = entitySOData.unitdata.AttackRangeEnd;
        useData.Cost = entitySOData.unitdata.Cost;
        useData.InitColdDown = entitySOData.unitdata.InitColdDown;
        useData.FrontDelay = entitySOData.unitdata.FrontDelay;
        useData.BackDelay = entitySOData.unitdata.BackDelay;
        useData.IsOneTarget = entitySOData.unitdata.IsOneTarget;
        useData.Money = entitySOData.unitdata.Money;

    }

    public virtual void TakeDamage(float damage)
    {
        CurrentHealth -= damage;
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    
    protected virtual void Attack()
    {

        float dir = useData.isCat ? -1f : 1f;
        if (useData.IsOneTarget)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.right * dir * (useData.AttackRangeStart * multiplier), Vector2.right * dir, useData.AttackRangeEnd * multiplier, TargetLayer);
            if (hit.collider == null) return;
            IDamagable damagable = hit.collider.GetComponent<IDamagable>();
            if (damagable == null) return;
            Debug.Log("���� ���: " + hit.collider.name + ", ���� Ÿ�ٷ��̾�: " + LayerMask.LayerToName(hit.collider.gameObject.layer));
            damagable.TakeDamage(useData.DefaultDamage, ElementalType.normal, transform);
        }
        else
        {
            RaycastHit2D[] hitList = Physics2D.RaycastAll(transform.position + Vector3.right * dir * (useData.AttackRangeStart * multiplier), Vector2.right * dir, useData.AttackRangeEnd * multiplier, TargetLayer);
            if (hitList.Length == 0) return;
            foreach (RaycastHit2D hit in hitList)
            {
                IDamagable damagable = hit.collider.GetComponent<IDamagable>();
                Debug.Log("���� ���: " + hit.collider.name + ", ���� Ÿ�ٷ��̾�: " + LayerMask.LayerToName(hit.collider.gameObject.layer));
                if (damagable == null) continue;
                damagable.TakeDamage(useData.DefaultDamage, ElementalType.normal, transform);
            }
        }
        animatorHit.gameObject.SetActive(true);
    }


    protected virtual void Die()
    {

        Destroy(gameObject);
    }

    public virtual void TakeDamage(float damage, ElementalType damageType, Transform attacker)
    {

    }
    public virtual void OnHit()
    {
        Attack();
    }
    public virtual void OnEndAttack()
    {
        isAttacking = false;
    }
    private void OnDrawGizmos()
    {

            float dir = entitySOData.unitdata.isCat ? -1f : 1f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.right * dir * entitySOData.unitdata.Range * multiplier);

    }
}
