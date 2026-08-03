using EGM.Core.Enums;

namespace EGM.Core.Interfaces
{
    public interface ILogger
    {
        void Log(LogTypeEnum type, string message);
        void Audit(string actor, string action, string oldValue, string newValue);
    }
}