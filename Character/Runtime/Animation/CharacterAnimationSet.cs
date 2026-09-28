using UnityEngine;

namespace Easy.Character.Animation
{
    [CreateAssetMenu]
    public sealed class CharacterAnimationSet : ScriptableObject
    {
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip forward;
        [SerializeField] private AnimationClip backward;
        [SerializeField] private AnimationClip left;
        [SerializeField] private AnimationClip right;
        [SerializeField] private AnimationClip aiming;
        [SerializeField] private AnimationClip crouch;
        [SerializeField] private AnimationClip sprint;
        [SerializeField] private AnimationClip jump;
        [SerializeField] private AnimationClip fall;

        public AnimationClip Idle => idle;
        public AnimationClip Forward => forward;
        public AnimationClip Backward => backward;
        public AnimationClip Left => left;
        public AnimationClip Right => right;
        public AnimationClip Aiming => aiming;
        public AnimationClip Crouch => crouch;
        public AnimationClip Sprint => sprint;
        public AnimationClip Jump => jump;
        public AnimationClip Fall => fall;
    }
}
