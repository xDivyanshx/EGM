using Xunit;
using Moq;
using EGM.Core.Services;
using EGM.Core.Interfaces;
using EGM.Core.Enums;

namespace EGM.Core.Tests
{
    public class StateManagerTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly StateManager _stateManager;

        public StateManagerTests()
        {
            _mockLogger = new Mock<ILogger>();
            _stateManager = new StateManager(_mockLogger.Object);
        }

        [Fact]
        public void TransitionTo_IdleToRunning_ShouldSucceed()
        {
            bool result = _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Starting Game");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.RUNNING, _stateManager.CurrentState);
        }

        [Fact]
        public void TransitionTo_RunningToUpdating_ShouldFail_InvalidTransition()
        {
            _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start");

            bool result = _stateManager.TransitionTo(EGMStateEnum.UPDATING, "Try Update");

            Assert.False(result);
            Assert.Equal(EGMStateEnum.RUNNING, _stateManager.CurrentState);
        }

        [Fact]
        public void ForceState_ShouldOverrideRules_ToMaintenance()
        {
            _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start");

            _stateManager.ForceState(EGMStateEnum.MAINTENANCE, "Door Open");

            Assert.Equal(EGMStateEnum.MAINTENANCE, _stateManager.CurrentState);
        }
    }
}