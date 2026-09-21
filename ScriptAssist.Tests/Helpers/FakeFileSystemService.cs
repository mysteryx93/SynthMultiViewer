using System.IO.Abstractions.TestingHelpers;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.ScriptAssist.Tests;

internal sealed class FakeFileSystemService : FileSystemService
{
    public FakeFileSystemService() : this(new Dictionary<string, MockFileData>())
    {
    }

    public FakeFileSystemService(IDictionary<string, MockFileData> files, string currentDirectory = "")
        : base(new MockFileSystem(files, currentDirectory))
    {
    }

    public FakeFileSystemService Add(string path, string contents)
    {
        EnsureDirectoryExists(path);
        File.WriteAllText(path, contents);
        return this;
    }
}
