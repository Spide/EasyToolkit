using Easy.BehaviourTree;

public class RemoveVariable<T, V> : Node<T, V> where T : IBlackboard<V>
    {
        V keyToDelete;
        public RemoveVariable(V key)
        {
            keyToDelete = key;

        }

        public override Result Run()
        {
            blackboard.SetVariable(keyToDelete, null);
            return Result.SUCCESS; 
        }
    }