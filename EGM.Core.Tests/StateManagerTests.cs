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
        public void InitialState_ShouldBeIdle()
        {
            Assert.Equal(EGMStateEnum.IDLE, _stateManager.CurrentState);
        }

        [Fact]
        public void TransitionTo_IdleToRunning_ShouldSucceed()
        {
            bool result = _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Starting Game");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.RUNNING, _stateManager.CurrentState);
        }

        [Fact]
        public void TransitionTo_IdleToUpdating_ShouldSucceed()
        {
            bool result = _stateManager.TransitionTo(EGMStateEnum.UPDATING, "Update");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.UPDATING, _stateManager.CurrentState);
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
        public void TransitionTo_SameState_ShouldReturnTrue_AndNotFireEvent()
        {
            int fireCount = 0;
            _stateManager.OnStateChanged += _ => fireCount++;

            // Already IDLE -> request IDLE.
            bool result = _stateManager.TransitionTo(EGMStateEnum.IDLE, "No-op");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.IDLE, _stateManager.CurrentState);
            Assert.Equal(0, fireCount); // "already in state" short-circuit must not notify
        }

        [Theory]
        [InlineData(EGMStateEnum.RUNNING)]   // RUNNING -> MAINTENANCE
        [InlineData(EGMStateEnum.IDLE)]      // IDLE    -> MAINTENANCE
        [InlineData(EGMStateEnum.UPDATING)]  // UPDATING-> MAINTENANCE
        public void TransitionTo_ToMaintenance_ShouldAlwaysBeAllowed(EGMStateEnum from)
        {
            if (from != EGMStateEnum.IDLE)
                _stateManager.TransitionTo(from, "setup");

            bool result = _stateManager.TransitionTo(EGMStateEnum.MAINTENANCE, "Safety");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.MAINTENANCE, _stateManager.CurrentState);
        }

        [Fact]
        public void TransitionTo_ToError_ShouldAlwaysBeAllowed()
        {
            _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start");

            bool result = _stateManager.TransitionTo(EGMStateEnum.ERROR, "Fault");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.ERROR, _stateManager.CurrentState);
        }

        [Fact]
        public void TransitionTo_ErrorToIdle_ShouldSucceed()
        {
            _stateManager.ForceState(EGMStateEnum.ERROR, "Fault");

            bool result = _stateManager.TransitionTo(EGMStateEnum.IDLE, "Reset");

            Assert.True(result);
            Assert.Equal(EGMStateEnum.IDLE, _stateManager.CurrentState);
        }

        [Fact]
        public void TransitionTo_MaintenanceToRunning_ShouldFail()
        {
            _stateManager.ForceState(EGMStateEnum.MAINTENANCE, "Door");

            bool result = _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Try play");

            Assert.False(result);
            Assert.Equal(EGMStateEnum.MAINTENANCE, _stateManager.CurrentState);
        }

        [Fact]
        public void ForceState_ShouldOverrideRules_ToMaintenance()
        {
            _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start");

            _stateManager.ForceState(EGMStateEnum.MAINTENANCE, "Door Open");

            Assert.Equal(EGMStateEnum.MAINTENANCE, _stateManager.CurrentState);
        }

        // --- Event (OnStateChanged) behaviour ---------------------------------
        // These lock in the fix that moved OnStateChanged OUTSIDE the lock.

        [Fact]
        public void TransitionTo_OnSuccess_ShouldFireEventOnce_WithNewState()
        {
            var received = new List<EGMStateEnum>();
            _stateManager.OnStateChanged += s => received.Add(s);

            _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start");

            Assert.Single(received);
            Assert.Equal(EGMStateEnum.RUNNING, received[0]);
        }

        [Fact]
        public void TransitionTo_OnInvalid_ShouldNotFireEvent()
        {
            _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start");

            int fireCount = 0;
            _stateManager.OnStateChanged += _ => fireCount++;

            _stateManager.TransitionTo(EGMStateEnum.UPDATING, "Invalid"); // rejected

            Assert.Equal(0, fireCount);
        }

        [Fact]
        public void ForceState_ShouldFireEventOnce()
        {
            var received = new List<EGMStateEnum>();
            _stateManager.OnStateChanged += s => received.Add(s);

            _stateManager.ForceState(EGMStateEnum.MAINTENANCE, "Door");

            Assert.Single(received);
            Assert.Equal(EGMStateEnum.MAINTENANCE, received[0]);
        }

        [Fact]
        public async Task Subscriber_ReentrantTransition_ShouldNotDeadlock()
        {
            // Regression test: OnStateChanged is fired OUTSIDE the lock, so a subscriber
            // is free to call back into the StateManager (read state or transition again)
            // without deadlocking. If the event were raised while holding the lock, this
            // re-entrant TransitionTo would block forever on the same non-reentrant lock.
            _stateManager.OnStateChanged += s =>
            {
                if (s == EGMStateEnum.RUNNING)
                {
                    // read state + trigger a follow-on transition from within the handler
                    _ = _stateManager.CurrentState;
                    _stateManager.TransitionTo(EGMStateEnum.IDLE, "auto-stop from handler");
                }
            };

            var work = Task.Run(() => _stateManager.TransitionTo(EGMStateEnum.RUNNING, "Start"));
            var finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.True(finished == work, "TransitionTo did not complete in time - possible deadlock (event fired inside lock).");
            // The re-entrant handler drove RUNNING -> IDLE.
            Assert.Equal(EGMStateEnum.IDLE, _stateManager.CurrentState);
        }
    }
}
