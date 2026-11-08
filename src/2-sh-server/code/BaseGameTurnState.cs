using System.Timers;
using Jay.FSM;
using Timer = System.Threading.Timer;

namespace HelloServer.State;

public abstract class BaseGameTurnState : IUpdatableState
{
    public StateMachine<IUpdatableState> stateMachine;
    public GameManager gameManager { get; set; }

    private DateTime startTime;
    public int currentMsTime;
    public float MaxMsTime{get;set;}
    // 자식 클래스가 분기 없이 시간으로만 넘어갈 경우, 다음 상태를 오버라이드 해서 사용한다.
    protected virtual Type NextState => null;
    public BaseGameTurnState(StateMachine<IUpdatableState> stateMachine,GameManager gameManager, float MaxTime)
    {
        this.stateMachine = stateMachine;
        this.gameManager = gameManager;
        this.MaxMsTime = MaxTime * 1000;
    }



    public virtual void Enter()
    {
        
        startTime = DateTime.Now;

        Console.WriteLine($"[스테이트 머신] 현재 Enter 상태: {stateMachine.CurrentState} , 현재 사이클 {gameManager.currentCycle} , 현재 라운드 {gameManager.currentRound} , 제한시간(ms): {MaxMsTime}");
     
    }
    public string GetGameStateString()
    {
        return $"{stateMachine.CurrentState?.ToString() ?? "null"}";
    }

    public virtual void Exit()
    {
        currentMsTime = 0;


    }
    public virtual void Tick()
    {
        currentMsTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
        if(currentMsTime % 5000 == 0)
            Console.WriteLine($"[{gameManager?.currentRoom?.code}][스테이트 머신][{GetGameStateString()?? "null"}] 현재 Timer 상태: {currentMsTime}");
        if (currentMsTime > MaxMsTime && NextState != null)
            stateMachine.ChangeState(NextState);

        
    }

    protected void BroadcastAsync(object message, string exceptId = null)
    {
        // Enter()가 void라 await을 못 한다. 대신 예외가 조용히 사라지지 않게 여기서 붙잡는다.
        _ = gameManager.currentRoom.BroadcastAsync(message, exceptId)
            .ContinueWith(t => Console.WriteLine(
                    $"[{gameManager.currentRoom.code}] 상태 메시지 전송 실패 : {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
    }
    protected void SendAsync(Room.Member member, object message)
    { 
        gameManager.currentRoom.SendAsync(member, message) .ContinueWith(t => Console.WriteLine(
                $"[{gameManager.currentRoom.code}] 상태 메시지 전송 실패 : {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }
}