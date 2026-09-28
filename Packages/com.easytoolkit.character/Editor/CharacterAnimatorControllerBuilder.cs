#if UNITY_EDITOR
using System;
using Easy.Character.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Easy.Character.Editor
{
    public static class CharacterAnimatorControllerBuilder
    {
        public static AnimatorController Build(
            CharacterAnimationSet animationSet,
            CharacterAnimationProfile profile,
            string assetPath)
        {
            if (animationSet == null || profile == null || string.IsNullOrWhiteSpace(assetPath))
                return null;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            controller = controller != null
                ? controller
                : AnimatorController.CreateAnimatorControllerAtPath(assetPath);
            if (controller == null)
                return null;

            EnsureParameter(controller, profile.MovementSpeedParameter, AnimatorControllerParameterType.Float, 0f);
            EnsureParameter(controller, profile.MovementSpeedMultiplierParameter, AnimatorControllerParameterType.Float, 1f);
            EnsureParameter(controller, profile.MovementDirectionXParameter, AnimatorControllerParameterType.Float, 0f);
            EnsureParameter(controller, profile.MovementDirectionYParameter, AnimatorControllerParameterType.Float, 0f);
            EnsureParameter(controller, profile.AimingParameter, AnimatorControllerParameterType.Bool, 0f);
            EnsureParameter(controller, profile.CrouchedParameter, AnimatorControllerParameterType.Bool, 0f);
            EnsureParameter(controller, profile.SprintingParameter, AnimatorControllerParameterType.Bool, 0f);
            EnsureParameter(controller, profile.JumpingParameter, AnimatorControllerParameterType.Bool, 0f);
            EnsureParameter(controller, profile.FallingParameter, AnimatorControllerParameterType.Bool, 0f);

            int movementLayerIndex = EnsureLayer(controller, profile.MovementLayer, useBaseLayer: true);
            AnimatorControllerLayer movementLayer = controller.layers[movementLayerIndex];
            AnimatorState movement = EnsureState(movementLayer.stateMachine, "Directional Movement");
            BlendTree tree = movement.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree { name = "Directional Movement" };
                AssetDatabase.AddObjectToAsset(tree, controller);
                movement.motion = tree;
            }
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = profile.MovementDirectionXParameter;
            tree.blendParameterY = profile.MovementDirectionYParameter;
            tree.useAutomaticThresholds = false;
            tree.children = Array.Empty<ChildMotion>();
            AddDirectionalClip(tree, animationSet.Idle, Vector2.zero);
            AddDirectionalClip(tree, animationSet.Forward, Vector2.up);
            AddDirectionalClip(tree, animationSet.Backward, Vector2.down);
            AddDirectionalClip(tree, animationSet.Left, Vector2.left);
            AddDirectionalClip(tree, animationSet.Right, Vector2.right);
            movement.motion = tree;
            movement.speedParameterActive = true;
            movement.speedParameter = profile.MovementSpeedMultiplierParameter;
            movementLayer.defaultWeight = 1f;
            movementLayer.stateMachine.defaultState = movement;
            AnimatorControllerLayer[] layers = controller.layers;
            layers[movementLayerIndex] = movementLayer;
            controller.layers = layers;

            AddStaticLayer(controller, profile.AimingLayer, animationSet.Aiming, profile.MovementSpeedMultiplierParameter);
            AddStaticLayer(controller, profile.CrouchLayer, animationSet.Crouch, profile.MovementSpeedMultiplierParameter);
            AddStaticLayer(controller, profile.SprintLayer, animationSet.Sprint, profile.MovementSpeedMultiplierParameter);
            AddStaticLayer(controller, profile.JumpLayer, animationSet.Jump, null);
            AddStaticLayer(controller, profile.FallLayer, animationSet.Fall, null);
            AddIkLayer(controller, CharacterAimIkDriver.BodyLayerName);
            AddIkLayer(controller, CharacterAimIkDriver.HandsLayerName);
            EnsureAimIkLayerOrder(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType type,
            float defaultValue)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != parameterName)
                    continue;
                if (parameters[i].type == type)
                {
                    if (type == AnimatorControllerParameterType.Float
                        && !Mathf.Approximately(parameters[i].defaultFloat, defaultValue))
                    {
                        parameters[i].defaultFloat = defaultValue;
                        controller.parameters = parameters;
                    }
                    return;
                }
                controller.RemoveParameter(i);
                break;
            }

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = parameterName,
                type = type,
                defaultFloat = defaultValue,
            });
        }

        private static void AddDirectionalClip(BlendTree tree, Motion motion, Vector2 position)
        {
            if (motion != null)
                tree.AddChild(motion, position);
        }

        private static void AddStaticLayer(
            AnimatorController controller,
            string layerName,
            Motion motion,
            string speedParameter)
        {
            int layerIndex = EnsureLayer(controller, layerName, useBaseLayer: false);
            AnimatorControllerLayer layer = controller.layers[layerIndex];
            AnimatorState state = EnsureState(layer.stateMachine, layerName);
            state.motion = motion;
            state.writeDefaultValues = true;
            if (!string.IsNullOrEmpty(speedParameter))
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParameter;
            }
            else
            {
                state.speedParameterActive = false;
                state.speedParameter = string.Empty;
            }
            layer.defaultWeight = 0f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.stateMachine.defaultState = state;
            AnimatorControllerLayer[] layers = controller.layers;
            layers[layerIndex] = layer;
            controller.layers = layers;
        }

        private static void AddIkLayer(AnimatorController controller, string layerName)
        {
            int layerIndex = EnsureLayer(controller, layerName, useBaseLayer: false);
            AnimatorControllerLayer layer = controller.layers[layerIndex];
            AnimatorState state = EnsureState(layer.stateMachine, layerName + " Empty");
            state.motion = null;
            state.writeDefaultValues = true;
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.iKPass = true;
            layer.stateMachine.defaultState = state;
            AnimatorControllerLayer[] layers = controller.layers;
            layers[layerIndex] = layer;
            controller.layers = layers;
        }

        private static void EnsureAimIkLayerOrder(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int bodyIndex = -1;
            int handsIndex = -1;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == CharacterAimIkDriver.BodyLayerName)
                    bodyIndex = i;
                else if (layers[i].name == CharacterAimIkDriver.HandsLayerName)
                    handsIndex = i;
            }

            if (bodyIndex < 0
                || handsIndex < 0
                || (bodyIndex == layers.Length - 2 && handsIndex == layers.Length - 1))
            {
                return;
            }

            AnimatorControllerLayer[] ordered = new AnimatorControllerLayer[layers.Length];
            int targetIndex = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                if (i != bodyIndex && i != handsIndex)
                    ordered[targetIndex++] = layers[i];
            }

            ordered[targetIndex++] = layers[bodyIndex];
            ordered[targetIndex] = layers[handsIndex];
            controller.layers = ordered;
        }

        private static int EnsureLayer(
            AnimatorController controller,
            string layerName,
            bool useBaseLayer)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName)
                    return i;
            }

            if (useBaseLayer && layers.Length > 0 && layers[0].name == "Base Layer")
            {
                layers[0].name = layerName;
                controller.layers = layers;
                return 0;
            }

            AnimatorStateMachine stateMachine = new()
            {
                name = layerName + " State Machine",
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = useBaseLayer ? 1f : 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = stateMachine,
            });
            return controller.layers.Length - 1;
        }

        private static AnimatorState EnsureState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == stateName)
                    return states[i].state;
            }
            return stateMachine.AddState(stateName);
        }
    }
}
#endif
