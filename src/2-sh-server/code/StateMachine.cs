using System;
using System.Collections.Generic;
using System.Linq;
using HelloServer;

namespace Jay.FSM
{
    public class StateMachine<TState> where TState : class, IState
    {
        private readonly List<TState> states = new List<TState>();

        public TState CurrentState { get; private set; }

        

        public StateMachine() { }

        public StateMachine(IEnumerable<TState> states)
        {
            AddRange(states);
        }

        public void Add(TState state)
        {
            if (state == null || states.Contains(state)) return;
            states.Add(state);
        }

        public void AddRange(IEnumerable<TState> range)
        {
            if (range == null) return;
            foreach (var s in range) Add(s);
        }

        public void StopStateMachine()
        {
            CurrentState?.Exit();
            CurrentState = null;
        }

        public void ChangeState<T>() where T : TState
        {
            ChangeTo(states.FirstOrDefault(s => s is T), typeof(T));
            
        }

        
        public void ChangeState(Type stateType)
        {
            ChangeTo(states.FirstOrDefault(s => stateType.IsInstanceOfType(s)), stateType);
        }

        private void ChangeTo(TState next, Type requested)
        {
            if (next == null)
            {
                Console.WriteLine($"[StateMachine] 등록되지 않은 상태: {requested?.Name}");
                return;
            }

             if (ReferenceEquals(next, CurrentState)) return;
            
            var prev = CurrentState;

     
            prev?.Exit();
            CurrentState = next;
            next.Enter();
        }

        public bool IsCurrent<T>() where T : TState
        {
            return CurrentState is T;
        }

        public void Tick()
        {
            if (CurrentState is IUpdatableState updatable)
            {
                updatable.Tick();
            }
        }
    }
}
