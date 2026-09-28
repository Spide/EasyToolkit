namespace Easy.BehaviourTree
{
    public class Parallel<T, V> : CompositeNode<T, V> where T : IBlackboard<V>
    {
        public override Result Run()
        {
            var success = 0;
            for (int i = 0; i < Childs.Count; i++)
            {
                var result = Childs[i].Run();

                if (result == Result.FAILED)
                    success--;
                if(result == Result.SUCCESS)
                    success++;
            }

            if (success == Childs.Count)
                return Result.SUCCESS;
            else if (success == -Childs.Count)
                return Result.FAILED;

            return Result.RUNNING;
        }
    }
    }