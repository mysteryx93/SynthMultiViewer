using System.IO.Abstractions;

namespace HanumanInstitute.ScriptAssist.Services;

/// <inheritdoc />
public class FileSystemService : IFileSystemService
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Creates a wrapper around <paramref name="fileSystemService"/>.
    /// </summary>
    public FileSystemService(IFileSystem fileSystemService)
    {
        _fileSystem = fileSystemService.CheckNotNull();
    }

    /// <inheritdoc />
    public IDirectory Directory => _fileSystem.Directory;
    /// <inheritdoc />
    public IDirectoryInfoFactory DirectoryInfo => _fileSystem.DirectoryInfo;
    /// <inheritdoc />
    public IDriveInfoFactory DriveInfo => _fileSystem.DriveInfo;
    /// <inheritdoc />
    public IFile File => _fileSystem.File;
    /// <inheritdoc />
    public IFileInfoFactory FileInfo => _fileSystem.FileInfo;
    /// <inheritdoc />
    public IFileStreamFactory FileStream => _fileSystem.FileStream;
    /// <inheritdoc />
    public IFileSystemWatcherFactory FileSystemWatcher => _fileSystem.FileSystemWatcher;
    /// <inheritdoc />
    public IFileVersionInfoFactory FileVersionInfo => _fileSystem.FileVersionInfo;
    /// <inheritdoc />
    public IPath Path => _fileSystem.Path;

    /// <inheritdoc />
    public virtual void EnsureDirectoryExists(string path)
    {
        if (_fileSystem.Path.IsPathRooted(path))
        {
            var directory = Path.GetDirectoryName(path);
            if (directory.HasValue())
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <inheritdoc />
    public virtual void DeleteFileSilent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (System.IO.IOException) { }
    }

    /// <inheritdoc />
    public virtual IEnumerable<string> GetFilesByExtensions(string path, IEnumerable<string> extensions,
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
    {
        path.CheckNotNullOrEmpty();
        extensions.CheckNotNull();

        try
        {
            return Directory.EnumerateFiles(path, "*", searchOption)
                .Where(file => extensions.Any(extension =>
                    file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
        catch (System.IO.DirectoryNotFoundException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.IO.PathTooLongException) { }
        catch (System.IO.IOException) { }

        return [];
    }

    /// <inheritdoc />
    public virtual string GetPathWithoutExtension(string path)
    {
        path.CheckNotNullOrEmpty();
        var directory = Path.GetDirectoryName(path);
        return directory.HasValue()
            ? Path.Combine(directory, Path.GetFileNameWithoutExtension(path))
            : Path.GetFileNameWithoutExtension(path);
    }

    /// <inheritdoc />
    public virtual string GetPathWithFinalSeparator(string path)
    {
        path.CheckNotNullOrEmpty();
        if (!path.EndsWith(Path.DirectorySeparatorChar))
        {
            path += Path.DirectorySeparatorChar;
        }

        return path;
    }

    /// <inheritdoc />
    public virtual string SanitizeFileName(string fileName, char replacementChar = '_')
    {
        var blackList = new HashSet<char>(Path.GetInvalidFileNameChars()) { '"' };
        var output = fileName.ToCharArray();
        for (var i = 0; i < output.Length; i++)
        {
            if (blackList.Contains(output[i]))
            {
                output[i] = replacementChar;
            }
        }

        return new string(output);
    }
}
