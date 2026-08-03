using EGM.Core.Enums;
using EGM.Core.Interfaces;

namespace EGM.Core.Services
{
    public class BillValidatorService : IBillValidator, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IStateManager _stateManager;

        private Timer? _heartbeatTimer;
        private volatile bool _ackReceived;
        private bool _isSimulatedFailure;
        private bool _isRunning;

        private const int PingIntervalMs = 10000;
        private const int TimeoutMs = 2000;

        public BillValidatorService( ILogger logger, IStateManager stateManager)
        {
            _logger = logger;
            _stateManager = stateManager;
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _logger.Log(LogTypeEnum.Info, "Bill Validator Service Started.");

            _heartbeatTimer = new Timer(async _ => await PingLoop(), null,0,PingIntervalMs);
        }

        public void Stop()
        {
            _isRunning = false;
            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _heartbeatTimer?.Dispose();
        }

        public void ReceiveAck()
        {
            if (_isSimulatedFailure)
            {
                _logger.Log(LogTypeEnum.Warning,"Bill Validator ACK ignored (Simulated Failure Active).");
                return;
            }

            _ackReceived = true;
            _logger.Log(LogTypeEnum.Info, "Bill Validator ACK received.");
        }

        public void SetSimulatedFailure(bool shouldFail)
        {
            _isSimulatedFailure = shouldFail;

            string status = shouldFail ? "BROKEN (No ACKs)": "WORKING";

            _logger.Log(LogTypeEnum.Warning,$"Bill Validator simulation set to: {status}");
        }

        private async Task PingLoop()
        {
            if (!_isRunning)
                return;

            _ackReceived = false;

            _logger.Log(LogTypeEnum.Info,"[Hardware] Pinging Bill Validator...");

            try
            {
                if (!_isSimulatedFailure)
                {
                    await Task.Delay(500);
                    ReceiveAck();
                }

                await Task.Delay(TimeoutMs);

                CheckForAck();
            }
            catch (Exception ex)
            {
                _logger.Log(LogTypeEnum.Error,$"Bill Validator internal error: {ex.Message}");
            }
        }

        private void CheckForAck()
        {
            if (_ackReceived)
                return;

            _logger.Log(LogTypeEnum.Error, "[Hardware] Bill Validator Timeout! No ACK received.");

            // Idempotent state transition
            if (_stateManager.CurrentState != EGMStateEnum.MAINTENANCE)
            {
                _stateManager.ForceState(EGMStateEnum.MAINTENANCE, "Bill Validator Hardware Failure");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
