using UnityEngine;

public class Idle : Unit_State
{
    public Idle(StateMachin stateMachine, string animBoolName, Unit unit) : base(stateMachine, animBoolName, unit)
    {
    }

    public override void Enter()
    {
        base.Enter();
        // Additional logic for entering the Idle state
        stateTimer = entity.useData.FrontDelay;

        Debug.Log("Enter Idle State");
    }
    public override void Update()
    {
        base.Update();
        stateTimer -= Time.deltaTime;
        RaycastHit2D hit = Physics2D.Raycast(entity.transform.position, Vector2.right * (entity.useData.isCat ? -1f : 1f), entity.useData.Range * entity.multiplier, entity.TargetLayer);
        if(hit.collider == null )
        {
            Debug.Log("사정거리 내에 존재하는 적이 없으므로 이동 상태로 전환합니다.");
            stateMachine.ChangeState(entity.WalkState);
            return;
        }
        else if( hit.collider != null && stateTimer <= 0 )
        {
            stateMachine.ChangeState(entity.AttackState);
        }
        else if (stateTimer <= 0)
        {
            stateMachine.ChangeState(entity.WalkState);
        }
    }

}
