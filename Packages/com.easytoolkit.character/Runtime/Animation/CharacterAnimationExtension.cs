namespace Easy.Character.Animation
{
    public readonly struct CharacterAnimationExtensionState
    {
        public CharacterAnimationExtensionState(
            bool suppressLocomotionLayers,
            bool forceNormalPlayback = false)
        {
            SuppressLocomotionLayers = suppressLocomotionLayers;
            ForceNormalPlayback = forceNormalPlayback;
        }

        public bool SuppressLocomotionLayers { get; }
        public bool ForceNormalPlayback { get; }
    }

    public interface ICharacterAnimationExtensionStateSource
    {
        int Priority { get; }

        bool TryGetAnimationExtensionState(
            out CharacterAnimationExtensionState state);
    }
}
