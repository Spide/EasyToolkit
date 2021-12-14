using System;

namespace Easy.StateMachine
{
    public class Transition : Transition<IState>
    {
        public Transition(IState to, Func<bool> condition, Action onTransitionDone = null) : base(to, condition, onTransitionDone)
        {
        }

        public Transition(IState from, IState to, Func<bool> condition, Action onTransitionDone = null) : base(from, to, condition, onTransitionDone)
        {
        }
    }
    public class Transition<T> where T : IState
    {
        public Func<bool> Condition { get; }
        public Action OnTransitionDone { get; }
        public T To { get; }
        public T From { get; }

        public Transition(T to, Func<bool> condition, Action onTransitionDone = null)
        {
            To = to;
            Condition = condition;
            OnTransitionDone = onTransitionDone;
        }

        public Transition(T from, T to, Func<bool> condition, Action onTransitionDone = null)
        {
            From = from;
            To = to;
            Condition = condition;
            OnTransitionDone = onTransitionDone;
        }
    }
}