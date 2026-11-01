namespace AtcSoft.Log.Viewer.RecentFiles;

/// <summary>
/// Reads and writes the list of recently opened profile files (RecentOpenFiles.json).
/// </summary>
/// <remarks>
/// The layout must stay the same as in earlier versions (Atc.Wpf <c>RecentOpenFilesOption</c>):
/// <c>{ "RecentOpenFiles": [ { "TimeStamp": ..., "FilePath": ... } ] }</c>.
/// </remarks>
public static class RecentFilesFile
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Reads the list: newest first, one entry per file, only files that still exist.
    /// </summary>
    /// <param name="file">The RecentOpenFiles.json file.</param>
    /// <returns>The recent files; empty when the file is missing or cannot be read.</returns>
    public static IReadOnlyList<RecentFile> Load(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        file.Refresh();
        if (!file.Exists)
        {
            return [];
        }

        try
        {
            var content = JsonSerializer.Deserialize<RecentFilesContent>(File.ReadAllText(file.FullName), SerializerOptions);
            return Clean(content?.RecentOpenFiles.Select(x => new RecentFile(x.TimeStamp, x.FilePath)) ?? []);
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>
    /// Puts a file first in the list and saves the list.
    /// </summary>
    /// <param name="file">The RecentOpenFiles.json file; its folder is created when missing.</param>
    /// <param name="filePath">The profile file that was opened.</param>
    /// <param name="timeStamp">When it was opened.</param>
    /// <returns>The list as saved.</returns>
    public static IReadOnlyList<RecentFile> Add(
        FileInfo file,
        string filePath,
        DateTime timeStamp)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var recentFiles = Clean(Load(file).Prepend(new RecentFile(timeStamp, filePath)));

        var content = new RecentFilesContent
        {
            RecentOpenFiles = [.. recentFiles.Select(x => new RecentFileItem { TimeStamp = x.TimeStamp, FilePath = x.FilePath })],
        };

        Directory.CreateDirectory(file.DirectoryName!);
        File.WriteAllText(file.FullName, JsonSerializer.Serialize(content, SerializerOptions));

        return recentFiles;
    }

    private static List<RecentFile> Clean(IEnumerable<RecentFile> recentFiles)
        => recentFiles
            .Where(x => !string.IsNullOrEmpty(x.FilePath) && File.Exists(x.FilePath))
            .OrderByDescending(x => x.TimeStamp)
            .DistinctBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = JsonSerializerOptionsFactory.Create();
        options.PropertyNamingPolicy = null;
        options.PropertyNameCaseInsensitive = true;
        return options;
    }

    private sealed class RecentFilesContent
    {
        public List<RecentFileItem> RecentOpenFiles { get; set; } = [];
    }

    private sealed class RecentFileItem
    {
        public DateTime TimeStamp { get; set; }

        public string FilePath { get; set; } = string.Empty;
    }
}