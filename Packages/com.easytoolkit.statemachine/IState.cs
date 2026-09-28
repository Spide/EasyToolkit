namespace Easy.StateMachine
{
    public interface IState
    {
        void Update();
        void Enter();
        void Exit();
    }
}