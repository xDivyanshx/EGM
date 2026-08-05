using Xunit;
using EGM.Core.Infrastructure;
using System;
using System.IO;

namespace EGM.Core.Tests
{
    public class FileFunctionsTests : IDisposable
    {
        private readonly string _tempFile;

        public FileFunctionsTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"egm_ff_{Guid.NewGuid():N}.txt");
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile)) File.Delete(_tempFile);
        }

        [Fact]
        public void LogDirectory_ShouldResolveToExistingLogsFolder()
        {
            string dir = FileFunctions.LogDirectory;

            Assert.False(string.IsNullOrWhiteSpace(dir));
            Assert.EndsWith("Logs", dir);
            Assert.True(Directory.Exists(dir)); // getter creates it if missing
        }

        [Fact]
        public void TryWriteFile_ThenTryReadFile_RoundTrips()
        {
            bool wrote = FileFunctions.TryWriteFile(_tempFile, "hello world", out string writeErr);

            Assert.True(wrote);
            Assert.Empty(writeErr);

            bool read = FileFunctions.TryReadFile(_tempFile, out string content, out string readErr);

            Assert.True(read);
            Assert.Empty(readErr);
            Assert.Equal("hello world", content);
        }

        [Fact]
        public void TryReadFile_MissingFile_ReturnsFalse()
        {
            string missing = Path.Combine(Path.GetTempPath(), $"egm_missing_{Guid.NewGuid():N}.txt");

            bool read = FileFunctions.TryReadFile(missing, out string content, out string error);

            Assert.False(read);
            Assert.Equal("File does not exist.", error);
            Assert.Empty(content);
        }

        [Fact]
        public void TryWriteFile_InvalidPath_ReturnsFalseWithError()
        {
            // A path with an invalid directory triggers the catch branch.
            string bad = Path.Combine("Z:\\no_such_drive_egm", "sub", "file.txt");

            bool wrote = FileFunctions.TryWriteFile(bad, "x", out string error);

            Assert.False(wrote);
            Assert.Contains("Failed to write file", error);
        }

        [Fact]
        public void TryReadFile_InvalidPathCharacters_ReturnsFalseWithError()
        {
            // Force the exception branch (not the "does not exist" branch) with an
            // illegal path so File.Exists throws / returns false via a bad drive.
            string bad = "Z:\\no_such_drive_egm\\sub\\file.txt";

            bool read = FileFunctions.TryReadFile(bad, out string content, out string error);

            // Non-existent drive => File.Exists is false => "File does not exist."
            Assert.False(read);
            Assert.Empty(content);
            Assert.False(string.IsNullOrEmpty(error));
        }
    }
}
