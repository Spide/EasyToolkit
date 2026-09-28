
using System;
using System.Collections;
using Easy.Logging;
using Easy.Pooling;
using UnityEngine;

namespace Easy
{
    public class EasyToolkitLoggerExamples : MonoBehaviour
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(EasyToolkitLoggerExamples), Color.red);

        void Start()
        {
            StartCoroutine(logInterval());
        }

        IEnumerator logInterval()
        {
            while (true)
            {
                LOGGER.Log("LOG in update");
                LOGGER.LogWarning("WARNING in update");
                LOGGER.LogError("ERROR in update");
                LOGGER.Assert("ASSERT in update");
                LOGGER.LogException(new Exception("EXCEPTION in update"));
                yield return new WaitForSeconds(1);
            }
        }
    }
}
