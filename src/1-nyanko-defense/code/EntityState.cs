using UnityEngine;

public class EntityState
{
    protected StateMachin stateMachine;
    protected string AnimBoolName;


    protected Animator animator;
    protected float stateTimer;
    protected bool triggerCalled;

    public EntityState(StateMachin stateMachine, string animBoolName)
    {
        this.stateMachine = stateMachine;

        this.AnimBoolName = animBoolName;


    }
    public virtual void Enter()
    {

        animator.SetBool(AnimBoolName, true);
        triggerCalled = false;
    }
    public virtual void Update()
    {

        UpdateAinmationParameters();
    }

    public virtual void Exit()
    {

        animator.SetBool(AnimBoolName, false);

    }
    public void CallAnimationTrigger()
    {
        if (triggerCalled)
            return;
        triggerCalled = true;
        animator.SetTrigger(AnimBoolName);
    }
    public virtual void UpdateAinmationParameters()
    {

    }
}
