using Xunit;
using EGM.Core.Validators;
using System;

namespace EGM.Core.Tests
{
    public class TimeZoneValidatorTests
    {
        private readonly TimeZoneValidator _validator = new();

        [Fact]
        public void ValidateTimeZone_ValidId_ReturnsTrue()
        {
            // Pick a real system zone so the test is portable across machines.
            var anyRealZone = TimeZoneInfo.GetSystemTimeZones()[0].Id;

            bool result = _validator.ValidateTimeZone(anyRealZone, out string error);

            Assert.True(result);
            Assert.Empty(error);
        }

        [Fact]
        public void ValidateTimeZone_CaseInsensitive_ReturnsTrue()
        {
            var zoneId = TimeZoneInfo.GetSystemTimeZones()[0].Id;

            bool result = _validator.ValidateTimeZone(zoneId.ToUpperInvariant(), out string error);

            Assert.True(result);
            Assert.Empty(error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ValidateTimeZone_EmptyOrWhitespace_ReturnsFalse(string? input)
        {
            bool result = _validator.ValidateTimeZone(input!, out string error);

            Assert.False(result);
            Assert.Equal("Timezone cannot be empty.", error);
        }

        [Fact]
        public void ValidateTimeZone_UnknownId_ReturnsFalse()
        {
            bool result = _validator.ValidateTimeZone("Mars/Olympus_Mons", out string error);

            Assert.False(result);
            Assert.Contains("Invalid timezone", error);
        }
    }
}
