namespace AtcSoft.Log.Viewer;

/// <summary>
/// Copies the data of earlier versions (machine-wide, in ProgramData) to the per-user folder once.
/// </summary>
public static class LegacyDataMigrator
{
    private const string RecentOpenFilesFileName = "RecentOpenFiles.json";

    /// <summary>
    /// Copies <paramref name="legacyRoot"/> to <paramref name="newRoot"/> when the new folder does not exist yet.
    /// </summary>
    /// <remarks>
    /// The legacy folder is never changed or deleted. Recent file paths that point into the legacy folder are
    /// rewritten to the new folder, so saving a recent profile writes the copy. The copy is made in a temporary
    /// folder and then renamed, so a failed copy is retried on the next start.
    /// </remarks>
    /// <param name="legacyRoot">The folder of earlier versions.</param>
    /// <param name="newRoot">The per-user folder.</param>
    /// <returns><see langword="true"/> when the data was copied.</returns>
    public static bool CopyIfNeeded(
        DirectoryInfo legacyRoot,
        DirectoryInfo newRoot)
    {
        ArgumentNullException.ThrowIfNull(legacyRoot);
        ArgumentNullException.ThrowIfNull(newRoot);

        legacyRoot.Refresh();
        newRoot.Refresh();
        if (newRoot.Exists || !legacyRoot.Exists)
        {
            return false;
        }

        var staging = new DirectoryInfo(newRoot.FullName + ".copying");
        if (staging.Exists)
        {
            staging.Delete(recursive: true);
        }

        CopyDirectory(legacyRoot, staging);
        RewriteRecentOpenFiles(
            new FileInfo(Path.Combine(staging.FullName, RecentOpenFilesFileName)),
            legacyRoot,
            newRoot);

        Directory.Move(staging.FullName, newRoot.FullName);
        return true;
    }

    private static void CopyDirectory(
        DirectoryInfo source,
        DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        foreach (var file in source.EnumerateFiles())
        {
            file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        foreach (var directory in source.EnumerateDirectories())
        {
            CopyDirectory(directory, new DirectoryInfo(Path.Combine(target.FullName, directory.Name)));
        }
    }

    private static void RewriteRecentOpenFiles(
        FileInfo recentOpenFiles,
        DirectoryInfo legacyRoot,
        DirectoryInfo newRoot)
    {
        if (!recentOpenFiles.Exists)
        {
            return;
        }

        JsonNode? json;
        try
        {
            json = JsonNode.Parse(File.ReadAllText(recentOpenFiles.FullName));
        }
        catch (JsonException)
        {
            // Keep an unreadable file as it is; the app treats it as no recent files.
            return;
        }

        if (json?["RecentOpenFiles"] is not JsonArray items)
        {
            return;
        }

        var legacyPrefix = Path.TrimEndingDirectorySeparator(legacyRoot.FullName) + Path.DirectorySeparatorChar;
        foreach (var item in items)
        {
            if (item?["FilePath"] is not JsonValue value ||
                !value.TryGetValue<string>(out var filePath) ||
                !filePath.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            item["FilePath"] = Path.Combine(newRoot.FullName, filePath[legacyPrefix.Length..]);
        }

        File.WriteAllText(
            recentOpenFiles.FullName,
            json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}