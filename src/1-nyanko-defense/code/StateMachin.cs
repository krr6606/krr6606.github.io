
public class StateMachin
{
    public EntityState currentState { get; private set; }
    public bool canChangeState;
    public void Initialize(EntityState startState)
    {
        canChangeState = true;
        currentState = startState;
        currentState.Enter();
    }
    public void ChangeState(EntityState newState)
    {
        if (!canChangeState)
        {
            return;
        }
        currentState.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void UpdateActiveState()
    {
        currentState.Update();
    }
    public void SwithOffStateMachine()
    {
        canChangeState = false;
    }
}

