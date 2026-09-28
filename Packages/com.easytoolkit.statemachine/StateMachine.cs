using System;
using System.Collections.Generic;
namespace Easy.StateMachine
{
    public class StateMachine : StateMachine<IState>
    { }
    public class StateMachine<T> where T : class, IState
    {
        private readonly Dictionary<Type, List<Transition<T>>> transitions = new Dictionary<Type, List<Transition<T>>>();
        protected List<Transition<T>> currentTransitions = new List<Transition<T>>();
        private readonly List<Transition<T>> anyTransitions = new List<Transition<T>>();

        private static readonly List<Transition<T>> EmptyTransitions = new List<Transition<T>>(0);

        public T CurrentState { get; protected set; }

        public event Action<T, T> OnStateChanged;

        public void Tick()
        {
            var transition = GetTransition();
            if (transition != null)
            {
                SetState(transition.To);
                transition.OnTransitionDone?.Invoke();
            }

            CurrentState?.Update();
        }

        public void Stop()
        {
            CurrentState?.Exit();
            CurrentState = null;
        }

        public void SetState(T state)
        {
            if (state == CurrentState)
                return;

            var previousState = CurrentState;
            previousState?.Exit();

            CurrentState = state;

            transitions.TryGetValue(CurrentState.GetType(), out currentTransitions);
            if (currentTransitions == null)
                currentTransitions = EmptyTransitions;

            CurrentState.Enter();

            OnStateChanged?.Invoke(previousState, CurrentState);
        }

        public Transition<T> AddTransition(T from, T to, Func<bool> predicate)
        {
            if (!transitions.TryGetValue(from.GetType(), out var rtransitions))
            {
                rtransitions = new List<Transition<T>>();
                transitions[from.GetType()] = rtransitions;
            }

            var transition = new Transition<T>(from, to, predicate);
            rtransitions.Add(transition);
            return transition;
        }

        public Transition<T> AddAnyTransition(T state, Func<bool> predicate, Action OnTransitionDone = null)
        {
            var transition = new Transition<T>(state, predicate, OnTransitionDone);
            anyTransitions.Add(transition);
            return transition;
        }

        private Transition<T> GetTransition()
        {
            foreach (var transition in anyTransitions)
            {
                if (transition.Condition())
                    return transition;
            }

            foreach (var transition in currentTransitions)
            {
                if (transition.Condition())
                    return transition;
            }

            return null;
        }

        public void RemoveTransition(Transition<T> transition)
        {
            if (transition.From != null)
            {
                transitions[transition.From.GetType()].Remove(transition);
            }
            else
            {
                anyTransitions.Remove(transition);
            }
        }
    }
}