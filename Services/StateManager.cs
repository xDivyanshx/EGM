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

                //  Execute Transition
                PerformTransition(newState, reason);
                return true;
            }
        }

        public void ForceState(EGMStateEnum newState, string reason)
        {
            lock (_lock)
            {
                _logger.Log(LogTypeEnum.Warning, $"[FORCE] Forcing state to {newState}. Reason: {reason}");
                PerformTransition(newState, reason);
            }
        }

        private void PerformTransition(EGMStateEnum newState, string reason)
        {
            var oldState = _currentState;
            _currentState = newState;

            _logger.Log(LogTypeEnum.Info, $"State Changed: {oldState} -> {newState} | Reason: {reason}");

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