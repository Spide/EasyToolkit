using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Easy.State;

namespace Easy.State
{
    public class StateUtils {
        public static int Sum<T>(T valueOf,params IState<T>[] states){
            int result = 0;

            foreach (var state in states)
                result += state.Get(valueOf);

            return result;
        }
    }
}
