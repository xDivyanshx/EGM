using System;
using EGM.Core.Interfaces;
using EGM.Core.Enums;

namespace EGM.Core.Services
{
    public class StateManager : IStateManager
    {
        private EGMStateEnum _currentState;
        private readonly ILogger _logger;
        private readonly object _lock = new(); 

        public event Action<EGMStateEnum>? OnStateChanged;

        public EGMStateEnum CurrentState
        {
            get { lock (_lock) return _currentState; }
        }

        public StateManager(ILogger logger)
        {
            _logger = logger;
            _currentState = EGMStateEnum.IDLE; // Initial State 
        }

        public bool TransitionTo(EGMStateEnum newState, string reason)
        {
            bool changed;

            lock (_lock)
            {
                // Check if we are already in the requested state
                if (_currentState == newState)
                {
                    _logger.Log(LogTypeEnum.Warning, $"Transition ignored: Already in {newState}.");
                    return true;
                }

                //  Validate Transition Rules
                if (!IsValidTransition(_currentState, newState))
                {
                    _logger.Log(LogTypeEnum.Warning, $"Invalid Transition: Cannot go from {_currentState} to {newState}. Reason: {reason}");
                    return false;
                }

                //  Execute Transition (mutates state + logs, but does NOT fire the event)
                PerformTransition(newState, reason);
                changed = true;
            }

            // Fire OnStateChanged OUTSIDE the lock: subscribers may read CurrentState,
            // request another transition, or take their own locks. Invoking while holding
            // _lock risks reentrancy (a subscriber mutating state mid-transition) and
            // cross-thread deadlock (lock-ordering inversion with a subscriber's lock).
            if (changed)
                NotifyStateChanged(newState);

            return true;
        }

        public void ForceState(EGMStateEnum newState, string reason)
        {
            lock (_lock)
            {
                _logger.Log(LogTypeEnum.Warning, $"[FORCE] Forcing state to {newState}. Reason: {reason}");
                PerformTransition(newState, reason);
            }

            // Same reasoning as TransitionTo: notify after releasing the lock.
            NotifyStateChanged(newState);
        }

        // Mutates state and logs the change. MUST be called while holding _lock.
        // Deliberately does not raise OnStateChanged - callers fire it after unlocking.
        private void PerformTransition(EGMStateEnum newState, string reason)
        {
            var oldState = _currentState;
            _currentState = newState;

            _logger.Log(LogTypeEnum.Info, $"State Changed: {oldState} -> {newState} | Reason: {reason}");
        }

        private void NotifyStateChanged(EGMStateEnum newState)
        {
            OnStateChanged?.Invoke(newState);
        }
        private static bool IsValidTransition(EGMStateEnum current, EGMStateEnum next)
        {
           // MAINTENANCE/ERROR can come from ANYWHERE (Safety First) 
            if (next == EGMStateEnum.MAINTENANCE || next == EGMStateEnum.ERROR) return true;

            // Define specific allow-lists
            switch (current)
            {
                case EGMStateEnum.IDLE:
                    return next == EGMStateEnum.RUNNING || next == EGMStateEnum.UPDATING;

                case EGMStateEnum.RUNNING:
                    return next == EGMStateEnum.IDLE; // Game finishes

                case EGMStateEnum.MAINTENANCE:
                    return next == EGMStateEnum.IDLE; // Technician fixed it

                case EGMStateEnum.UPDATING:
                    return next == EGMStateEnum.IDLE; // Update finished/rolled back

                case EGMStateEnum.ERROR:
                    return next == EGMStateEnum.IDLE; // Error cleared (reset)

                default:
                    return false;
            }
        }
    }
}