
namespace EGM.Core.Interfaces
{
    public interface IBillValidator
    {
        void Start();
        void Stop();

        // Simulates the hardware sending an ACK signal back to us
        void ReceiveAck();

        // Simulates breaking the device (for testing failure)
        void SetSimulatedFailure(bool shouldFail);
    }
}