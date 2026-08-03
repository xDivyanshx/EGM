using EGM.Core.Entities;

namespace EGM.Core.Interfaces
{
    public interface IConfigManager
    {
        SystemConfig GetConfig();
        void UpdateConfig(Action<SystemConfig> updateAction);
    }
}