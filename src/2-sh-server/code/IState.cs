using HelloServer;

namespace Jay.FSM
{
    public interface IState
    {
        GameManager gameManager { get; set; }
        public float MaxMsTime{get;set;}
        void Enter();
        void Exit();

        public string GetGameStateString();
    }

    public interface IUpdatableState : IState
    {
        void Tick();
    }
}
