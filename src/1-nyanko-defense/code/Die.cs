using DG.Tweening;
using UnityEngine;
public class Die : Unit_State
{
    public Die(StateMachin stateMachine, string animBoolName, Unit unit) : base(stateMachine, animBoolName, unit)
    {

    }
    public override void Enter()
    {
        base.Enter();
        entity.collider2d.enabled = false;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(entity.transform.DOShakePosition(5, new Vector3(0.2f, 0, 0f), 2, 90f, false, false).SetEase(Ease.Linear));
        sequence.Join(entity.transform.DOMoveY(entity.transform.position.y + 25f, 5).SetEase(Ease.Linear));
        sequence.OnComplete(() =>
        {
            if (entity.useData.isCat)
            {
                InGameEconomyManager.Instance.IncreaseGold(entity.useData.Money);
            }
            entity.gameObject.SetActive(false);
        });
    }

}

