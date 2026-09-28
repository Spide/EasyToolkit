#if UNITY_EDITOR
using Easy.Character.Animation;
using UnityEditor;
using UnityEngine;

namespace Easy.Character.Editor
{
    public sealed class CharacterAnimatorControllerBuilderWindow : EditorWindow
    {
        private CharacterAnimationSet animationSet;
        private CharacterAnimationProfile profile;

        [MenuItem("Easy Toolkit/Character/Build Animator Controller")]
        private static void Open()
        {
            GetWindow<CharacterAnimatorControllerBuilderWindow>("Character Animator");
        }

        private void OnGUI()
        {
            animationSet = (CharacterAnimationSet)EditorGUILayout.ObjectField(
                "Animation Set",
                animationSet,
                typeof(CharacterAnimationSet),
                false);
            profile = (CharacterAnimationProfile)EditorGUILayout.ObjectField(
                "Animation Profile",
                profile,
                typeof(CharacterAnimationProfile),
                false);

            using (new EditorGUI.DisabledScope(animationSet == null || profile == null))
            {
                if (!GUILayout.Button("Build Or Update Animator Controller"))
                    return;
                string path = EditorUtility.SaveFilePanelInProject(
                    "Build Or Update Character Animator Controller",
                    "CharacterAnimator",
                    "controller",
                    "Select the consumer-owned controller location.");
                if (!string.IsNullOrEmpty(path))
                    CharacterAnimatorControllerBuilder.Build(animationSet, profile, path);
            }
        }
    }
}
#endif
