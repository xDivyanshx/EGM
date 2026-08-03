using Xunit;
using EGM.Core.Validators;
using System;
using System.IO;

namespace EGM.Core.Tests
{
    public class PackageValidatorTests : IDisposable
    {
        private readonly PackageValidator _validator;
        private string _tempFilePath;

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
    }
}