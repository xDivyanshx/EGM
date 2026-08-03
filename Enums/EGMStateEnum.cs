
namespace EGM.Core.Enums
{
    public enum EGMStateEnum
    {
        IDLE,           // Default state
        RUNNING,        // Game is active
        MAINTENANCE,    // Door open or hardware failure
        UPDATING,       // Installing package
        ERROR           // Generic error state
    }
}
