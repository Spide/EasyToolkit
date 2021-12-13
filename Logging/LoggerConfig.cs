using UnityEngine;

namespace Easy.Logging
{
    [DefaultExecutionOrder(-10000)]
    public class LoggerConfig : MonoBehaviour
    {
        [SerializeField]
        private TextAsset configFile;

        private LoggerConfigData configData;

        [SerializeField]
        private bool doNotDestroy = false;
        private void Awake()
        {
            
            configFile ??= Resources.Load<TextAsset>("easylogger.config");
            configData = JsonUtility.FromJson<LoggerConfigData>(configFile.text);
            LoggerFactory.Config.Apply(configData);

            if(!doNotDestroy){
                Destroy(this);
            } else {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}