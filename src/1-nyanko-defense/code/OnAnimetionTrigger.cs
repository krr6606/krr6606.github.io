using UnityEngine;
using UnityEngine.UIElements;

public class OnAnimetionTrigger : MonoBehaviour
{
    public BaseUnit entity;
    private Collider2D attackCollider;
    private HeroSkill01 heroSkill01;

    private void Awake()
    {
        entity = GetComponent<BaseUnit>();
        attackCollider = GetComponent<Collider2D>();
        heroSkill01 = FindAnyObjectByType<HeroSkill01>();
    }
    public void OnAttackTrigger()
    { 
        entity.OnHit();
        entity.OnEndAttack();
    }

    public void OnEndAnimation()
    {
        gameObject.SetActive(false);
    }
    public void OnSkill01HitTrigger()
    {
        if(heroSkill01 == null)
        {
            Debug.LogError("HeroSkill01 is not assigned.");
            return;
        }
        Bounds box = attackCollider.bounds;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(box.center, box.size, 0f, Vector2.zero, 1f, heroSkill01.targetLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            int damage = heroSkill01.GetSkillDamage();
            damage += (int)(damage * 0.6 * (GameDataManager.Instance.userGameDataSO.levelDataList[(int)GameLevelDataName.skillDamageLevel].level - 1));
            hits[i].collider.GetComponent<IDamagable>()?.TakeDamage(damage, ElementalType.normal, transform);
            Debug.Log($"È÷Æ® {hits[i].collider.name} for {damage} damage.");
        }
    }

}
