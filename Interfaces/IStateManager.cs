using System;
using EGM.Core.Enums;

namespace EGM.Core.Interfaces
{
    public interface IStateManager
    {
        EGMStateEnum CurrentState { get; }

        // Attempts to change state. Returns true if successful.
        bool TransitionTo(EGMStateEnum newState, string reason);

        // Force a state change (ignoring rules) - strictly for Safety/Critical errors
        void ForceState(EGMStateEnum newState, string reason);

        // Event to notify other services when state changes
        event Action<EGMStateEnum> OnStateChanged;
    }
}