namespace Easy.BehaviourTree
{
    public class Selector<T, V> : CompositeNode<T, V> where T : IBlackboard<V>
    {
        public override Result Run()
        {
            for (int i = 0; i < Childs.Count; i++)
            {
                var result = Childs[i].Run();

                // if node is failed continue with ontherone
                if (result == Result.FAILED)
                    continue;
                else
                    return result; // report success or running immediatly

            }

            return Result.FAILED; // if all failed 
        }
    }

}