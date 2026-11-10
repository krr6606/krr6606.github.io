using System.Timers;
using Jay.FSM;
using NetworkManager;

namespace HelloServer.State;

public class DebateTimeState : BaseGameTurnState
{
    protected override Type NextState => typeof(DebateEndState);
    public DebateTimeState(StateMachine<IUpdatableState> stateMachine, GameManager gameManager, float maxTime) : base(stateMachine, gameManager, maxTime)
    {
    }

    public override void Enter()
    {
        base.Enter();

        BroadcastAsync(TurnMessageFactory.DebateTime(MaxMsTime,gameManager.currentCycle,gameManager.currentRound));
        
    }


    
}