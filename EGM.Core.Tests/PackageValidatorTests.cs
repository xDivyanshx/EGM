using Xunit;
using EGM.Core.Validators;
using System;
using System.IO;

namespace EGM.Core.Tests
{
    public class PackageValidatorTests : IDisposable
    {
        private readonly PackageValidator _validator;
        private string _tempFilePath = string.Empty;

        public PackageValidatorTests()
        {
            _validator = new PackageValidator();
        }

        public void Dispose()
        {
            if (File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }

        [Fact]
        public void Validate_CorrectFormatAndNewerVersion_ReturnsTrue()
        {
            _tempFilePath = Path.GetTempFileName();
            string validNamePath = Path.Combine(Path.GetDirectoryName(_tempFilePath)!, "update_pkg_2.0.0.txt");

            if (File.Exists(validNamePath)) File.Delete(validNamePath);
            File.Move(_tempFilePath, validNamePath);
            _tempFilePath = validNamePath;

            Version current = new Version(1, 0, 0);

            bool result = _validator.TryValidateAndExtractVersion(validNamePath, current, out Version newVer, out string error);

            Assert.True(result);
            Assert.Equal(new Version(2, 0, 0), newVer);
            Assert.Empty(error);
        }

        [Fact]
        public void Validate_OlderVersion_ReturnsFalse()
        {
            _tempFilePath = Path.GetTempFileName();
            string oldVerPath = Path.Combine(Path.GetDirectoryName(_tempFilePath)!, "update_pkg_0.5.0.txt");

            if (File.Exists(oldVerPath)) File.Delete(oldVerPath);
            File.Move(_tempFilePath, oldVerPath);
            _tempFilePath = oldVerPath;

            Version current = new Version(1, 0, 0);

            bool result = _validator.TryValidateAndExtractVersion(oldVerPath, current, out Version newVer, out string error);

            Assert.False(result);
            Assert.Equal("Downgrade or same version not allowed.", error);
        }

        [Fact]
        public void Validate_SameVersion_ReturnsFalse()
        {
            _tempFilePath = Path.GetTempFileName();
            string samePath = Path.Combine(Path.GetDirectoryName(_tempFilePath)!, "update_pkg_1.0.0.txt");

            if (File.Exists(samePath)) File.Delete(samePath);
            File.Move(_tempFilePath, samePath);
            _tempFilePath = samePath;

            Version current = new Version(1, 0, 0);

            bool result = _validator.TryValidateAndExtractVersion(samePath, current, out _, out string error);

            Assert.False(result);
            Assert.Equal("Downgrade or same version not allowed.", error);
        }

        [Fact]
        public void Validate_FileDoesNotExist_ReturnsFalse()
        {
            string nonExistent = "C:\\does_not_exist\\update_pkg_2.0.0.txt";

            bool result = _validator.TryValidateAndExtractVersion(nonExistent, new Version(1, 0), out _, out string error);

            Assert.False(result);
            Assert.Contains("does not exist", error);
        }

        [Fact]
        public void Validate_InvalidFormat_TooFewParts_ReturnsFalse()
        {
            _tempFilePath = Path.GetTempFileName();
            string badPath = Path.Combine(Path.GetDirectoryName(_tempFilePath)!, "bad_pkg.txt");

            if (File.Exists(badPath)) File.Delete(badPath);
            File.Move(_tempFilePath, badPath);
            _tempFilePath = badPath;

            bool result = _validator.TryValidateAndExtractVersion(badPath, new Version(1, 0), out _, out string error);

            Assert.False(result);
            Assert.Equal("Invalid package format. Expected update_pkg_x.y.z", error);
        }

        [Fact]
        public void Validate_InvalidVersionString_ReturnsFalse()
        {
            _tempFilePath = Path.GetTempFileName();
            string badVerPath = Path.Combine(Path.GetDirectoryName(_tempFilePath)!, "update_pkg_notAVersion.txt");

            if (File.Exists(badVerPath)) File.Delete(badVerPath);
            File.Move(_tempFilePath, badVerPath);
            _tempFilePath = badVerPath;

            bool result = _validator.TryValidateAndExtractVersion(badVerPath, new Version(1, 0), out _, out string error);

            Assert.False(result);
            Assert.Equal("Invalid version format.", error);
        }
    }
}
