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
        public void LogDirectory_ShouldResolveToLogsFolder()
        {
            string dir = FileFunctions.LogDirectory;

            Assert.False(string.IsNullOrWhiteSpace(dir));
            Assert.EndsWith("Logs", dir);
            // No Directory.Exists assertion here on purpose. This property is now
            // side-effect free, so whether the folder happens to exist is not part of
            // its contract - EnsureLogDirectory_ShouldCreateTheFolder covers creation.
        }

        [Fact]
        public void EnsureLogDirectory_ShouldCreateTheFolder()
        {
            string dir = FileFunctions.EnsureLogDirectory();

            Assert.EndsWith("Logs", dir);
            Assert.True(Directory.Exists(dir));
            // Same location as the pure property - the two must never diverge.
            Assert.Equal(FileFunctions.LogDirectory, dir);
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
        public void TryWriteFile_UnwritablePath_ReturnsFalseWithError()
        {
            // A path whose parent directory does not exist, which is what makes the
            // write throw. Deliberately NOT a "Z:\..." drive letter: on Linux that is a
            // perfectly legal file name, so the write would succeed and leave a junk
            // file in the working directory - and poison TryReadFile_MissingDirectory_
            // ReturnsFalse below, which reads the same path. This form fails identically
            // on every platform.
            string bad = Path.Combine(Path.GetTempPath(), $"egm_absent_{Guid.NewGuid():N}", "sub", "file.txt");

            bool wrote = FileFunctions.TryWriteFile(bad, "x", out string error);

            Assert.False(wrote);
            Assert.Contains("Failed to write file", error);
        }

        [Fact]
        public void TryReadFile_MissingDirectory_ReturnsFalse()
        {
            // File.Exists returns false (rather than throwing) for a path under a
            // directory that isn't there, so this exercises the "does not exist" branch.
            string bad = Path.Combine(Path.GetTempPath(), $"egm_absent_{Guid.NewGuid():N}", "sub", "file.txt");

            bool read = FileFunctions.TryReadFile(bad, out string content, out string error);

            Assert.False(read);
            Assert.Empty(content);
            Assert.Equal("File does not exist.", error);
        }
    }
}
