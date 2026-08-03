using EGM.Core.Interfaces;
namespace EGM.Core.Validators
{
    public class TimeZoneValidator : ITimeZoneValidator
    {
        public bool ValidateTimeZone(string timeZone, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(timeZone))
                errorMessage = "Timezone cannot be empty.";
            else
            {
                var zones = TimeZoneInfo.GetSystemTimeZones();

                if (!zones.Any(z => z.Id.Equals(timeZone, StringComparison.OrdinalIgnoreCase)))
                    errorMessage = $"Invalid timezone: {timeZone}";
            }
            return errorMessage == string.Empty;
        }
    }

}
