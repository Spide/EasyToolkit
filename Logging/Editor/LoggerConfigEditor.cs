using UnityEditor;
using UnityEngine;

namespace Easy.Logging.Editor
{
    [CustomEditor(typeof(LoggerConfig))]
    public class LoggerConfigEditor : UnityEditor.Editor
    {
        private bool toggle;

        string[] options = new string[]
        {
            LogType.Log.ToString(),
            LogType.Warning.ToString(),
            LogType.Error.ToString(),
            LogType.Assert.ToString(),
            LogType.Exception.ToString()
        };

        LogType[] optionTypes = new LogType[]
        {
            LogType.Log,
            LogType.Warning,
            LogType.Error,
            LogType.Assert,
            LogType.Exception
        };

        public int typeToOption(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    return 0;
                case LogType.Warning:
                    return 1;
                case LogType.Error:
                    return 2;
                case LogType.Assert:
                    return 3;
                case LogType.Exception:
                    return 4;
                default:
                    return 0;
            }
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var enabled = new GUIStyle(GUI.skin.button);
            enabled.normal.textColor = Color.green;

            var infobox = new GUIStyle(GUI.skin.box);
            infobox.normal.textColor = Color.gray;
            infobox.border =  new RectOffset(1,1,1,1);
            infobox.alignment = TextAnchor.MiddleCenter;
            infobox.stretchWidth = true;

            if(!Application.isPlaying){
               
                EditorGUILayout.BeginHorizontal("Label");
                GUILayout.Label("settings available in play mode", infobox);
                EditorGUILayout.EndHorizontal();
                return;
            } 

            EditorGUILayout.BeginHorizontal("Label");
            GUILayout.Label("Using this settings you can override logger settings (in current play mode session only)", infobox);
            EditorGUILayout.EndHorizontal();

            
            foreach (var item in LoggerFactory.Loggers)
            {
                EditorGUILayout.BeginHorizontal("Label");
                GUILayout.Label(string.Format("{0}", item.Key.Name));

                var currentOption = typeToOption(item.Value.filterLogType);
                var selected = EditorGUILayout.Popup(currentOption, options, GUILayout.Width(70f));
                if (selected != currentOption)
                {
                    item.Value.filterLogType = optionTypes[selected];
                }

                if (GUILayout.Button(item.Value.logEnabled ? "enabled" : "disabled", item.Value.logEnabled ?  enabled :new GUIStyle(GUI.skin.button), GUILayout.Width(70f)))
                {
                    item.Value.logEnabled = !item.Value.logEnabled;
                    Debug.LogFormat("LOGGER \"{0}, {1}\" is now {2} ", item.Key.FullName, item.Key.Assembly.FullName, item.Value.logEnabled ? "enabled" : "disabled" );
                }

                EditorGUILayout.EndHorizontal();
            }


        }
    }
}