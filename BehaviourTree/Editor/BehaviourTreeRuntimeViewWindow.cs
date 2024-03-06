using System;
using UnityEditor;
using UnityEngine;

namespace Easy.BehaviourTree.Editor
{
    /*
    [ExecuteInEditMode]
    public class BehaviourTreeRuntimeViewWindow : EditorWindow
    {
        [MenuItem("Window/BehaviourTree/View")]
        private static void OpenWindow()
        {
            GetWindow<BehaviourTreeRuntimeViewWindow>().Show();
        }

        void OnGUI()
        {
            AICharacter character;

            if (Selection.activeGameObject == null || !Selection.activeGameObject.TryGetComponent<AICharacter>(out character))
            {
                GUILayout.Label("Select character", EditorStyles.boldLabel);
                return;
            }


            if (!Application.isPlaying)
            {
                GUILayout.Label("Play mode only!", EditorStyles.boldLabel);
                return;
            }

            GUILayout.Label("--- State ---", EditorStyles.boldLabel);
            GUILayout.Label(character.State.ToString());
            GUILayout.Label("--- Knowledge ---", EditorStyles.boldLabel);
            GUILayout.Label(character.Behaviour.KnowledgeBase.ToString(), EditorStyles.boldLabel);
            GUILayout.Label("--- Behaviour ---", EditorStyles.boldLabel);

            resolveNode(character.Behaviour.StateMachine.CurrentState, null, "");

            //GUILayout.Label ("Updated"+ DateTime.Now);
        }

        public void Update()
        {
            // This is necessary to make the framerate normal for the editor window.
            Repaint();
        }


        Type nestedType = typeof(NestedCharacterBehaviour);
        public void resolveNode(ICharacterBehaviour node, NestedCharacterBehaviour parent, String path)
        {

            if(node == null)
            {
                GUILayout.Label(path + "null");
                return;
            }

            var score = parent != null && parent.IsActive ? node.UtilityScore() : -1;

            if (parent?.CurrentChildBehaviour == node)
            {
                GUI.color = Color.green;

                GUILayout.Label(path + (node == null ? "null" : node.GetType().Name) + " (" + score + ")", EditorStyles.boldLabel);
            }
            else
            {
                if (score < 0) GUI.color = Color.gray; else GUI.color = Color.black;
                GUILayout.Label(path + (node == null ? "null" : node.GetType().Name) + " (" + score + ")");
            }

            if (nestedType.IsAssignableFrom(node.GetType()))
            {
                NestedCharacterBehaviour behaviour = (NestedCharacterBehaviour)node;
                path = path + "\t";

                foreach (var item in behaviour.Childs)
                {
                    resolveNode(item, behaviour, path);
                }



            }
        }
    }*/
}