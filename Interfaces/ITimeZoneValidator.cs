
namespace EGM.Core.Interfaces
{
    public interface ITimeZoneValidator
    {
        bool ValidateTimeZone(string timeZone, out string errorMessage);
    }

}
