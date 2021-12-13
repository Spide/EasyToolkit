using System;

namespace Easy.Control
{
    public interface IControlContext
    {
        event Action<IControlContext, bool> OnActiveStatusChanged;
        int Priority { get; }
        bool IsActive { get; }

        void Activate();
        void Deactivate();

        bool On(string eventName, params object[] parameters);
    }
}