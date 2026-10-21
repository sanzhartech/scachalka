using YtDlpGui.Abstractions.Enums;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;
using YtDlpGui.Infrastructure.FileSystem;

namespace YtDlpGui.Core.Tests;

public sealed class FolderServiceTests
{
    private sealed class MockLog : ILogSink
    {
        public void Write(LogLevel level, string message) { }
#pragma warning disable CS0067
        public event EventHandler<LogEntry>? EntryAdded;
#pragma warning restore CS0067
        public IReadOnlyList<LogEntry> GetSnapshot() => [];
    }

    [Fact]
    public void RevealFile_ExistingFile_PassesCorrectSelectArgument()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            string? capturedArgs = null;
            var service = new FolderService(new MockLog())
            {
                ExplorerLauncher = args =>
                {
                    capturedArgs = args;
                    return true;
                }
            };

            var result = service.RevealFile(tempFile);

            Assert.True(result);
            Assert.NotNull(capturedArgs);
            var expectedPath = System.IO.Path.GetFullPath(tempFile);
            Assert.Equal($"/select,\"{expectedPath}\"", capturedArgs);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void RevealFile_MissingFile_FallsBackToExistingFolder()
    {
        var tempDir = System.IO.Path.GetTempPath();
        var missingFile = System.IO.Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".mp4");

        string? capturedArgs = null;
        var service = new FolderService(new MockLog())
        {
            ExplorerLauncher = args =>
            {
                capturedArgs = args;
                return true;
            }
        };

        var result = service.RevealFile(missingFile);

        Assert.True(result);
        Assert.NotNull(capturedArgs);
        // Fallback opens the folder, not /select
        var expectedFolder = System.IO.Path.GetFullPath(tempDir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        Assert.Equal($"\"{expectedFolder}\"", capturedArgs);
    }

    [Fact]
    public void RevealFile_UnicodePathAndCyrillic_PassesCorrectly()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Scachalka_Тест_Юникод_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var unicodeFile = System.IO.Path.Combine(tempDir, "Видео клип 2026.mp4");
        File.WriteAllText(unicodeFile, "dummy");

        try
        {
            string? capturedArgs = null;
            var service = new FolderService(new MockLog())
            {
                ExplorerLauncher = args =>
                {
                    capturedArgs = args;
                    return true;
                }
            };

            var result = service.RevealFile(unicodeFile);

            Assert.True(result);
            Assert.NotNull(capturedArgs);
            var expectedPath = System.IO.Path.GetFullPath(unicodeFile);
            Assert.Equal($"/select,\"{expectedPath}\"", capturedArgs);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void RevealFile_KazakhCharacters_PassesCorrectly()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Scachalka_Қазақша_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        // Kazakh specific chars: ә, і, ң, ғ, ү, ұ, қ, ө, һ
        var kazakhFile = System.IO.Path.Combine(tempDir, "ән_әіңғүұқөһ.mp3");
        File.WriteAllText(kazakhFile, "dummy audio");

        try
        {
            string? capturedArgs = null;
            var service = new FolderService(new MockLog())
            {
                ExplorerLauncher = args =>
                {
                    capturedArgs = args;
                    return true;
                }
            };

            var result = service.RevealFile(kazakhFile);

            Assert.True(result);
            Assert.NotNull(capturedArgs);
            var expectedPath = System.IO.Path.GetFullPath(kazakhFile);
            Assert.Equal($"/select,\"{expectedPath}\"", capturedArgs);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void RevealFile_SpecialCharsSpacesBracketsAndAmpersand_PassesCorrectly()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Scachalka & Music (HQ) [2026]_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var specialFile = System.IO.Path.Combine(tempDir, "Artist & Singer - Track (Remix) [1080p].mp4");
        File.WriteAllText(specialFile, "dummy");

        try
        {
            string? capturedArgs = null;
            var service = new FolderService(new MockLog())
            {
                ExplorerLauncher = args =>
                {
                    capturedArgs = args;
                    return true;
                }
            };

            var result = service.RevealFile(specialFile);

            Assert.True(result);
            Assert.NotNull(capturedArgs);
            var expectedPath = System.IO.Path.GetFullPath(specialFile);
            Assert.Equal($"/select,\"{expectedPath}\"", capturedArgs);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenFolder_ExistingFolder_PassesFolderArgument()
    {
        var tempDir = System.IO.Path.GetTempPath();
        string? capturedArgs = null;
        var service = new FolderService(new MockLog())
        {
            ExplorerLauncher = args =>
            {
                capturedArgs = args;
                return true;
            }
        };

        var result = service.OpenFolder(tempDir);

        Assert.True(result);
        Assert.NotNull(capturedArgs);
        var expected = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(tempDir));
        Assert.Equal($"\"{expected}\"", capturedArgs);
    }

    [Fact]
    public void OpenFolder_NonExistingFolder_ReturnsFalse()
    {
        var nonExisting = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NonExisting_" + Guid.NewGuid().ToString("N"));
        var service = new FolderService(new MockLog())
        {
            ExplorerLauncher = _ => true
        };

        var result = service.OpenFolder(nonExisting);
        Assert.False(result);
    }

    [Fact]
    public void OpenFolder_EmptyOrWhitespace_ReturnsFalse()
    {
        var service = new FolderService(new MockLog());
        Assert.False(service.OpenFolder(string.Empty));
        Assert.False(service.OpenFolder("   "));
    }
}
