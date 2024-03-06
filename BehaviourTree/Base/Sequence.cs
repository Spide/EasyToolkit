namespace Easy.BehaviourTree
{
    public class Sequence<T> : CompositeNode<T> where T : IBlackboard
    {
        public override Result Run()
        {
            for (int i = 0; i < Childs.Count; i++)
            {
                var result = Childs[i].Run();
                // if node is success continue with next
                if (result == Result.SUCCESS)
                    continue;
                else
                    return result; // report failed or running immediatly

            }

            return Result.SUCCESS; // if all success
        }
    }

}