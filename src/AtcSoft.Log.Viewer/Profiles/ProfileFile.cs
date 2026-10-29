namespace AtcSoft.Log.Viewer.Profiles;

/// <summary>
/// Reads and writes profile files (JSON).
/// </summary>
/// <remarks>
/// The format must stay the same as in earlier versions: property names as in <see cref="ProfileOptions"/>,
/// enums as strings and indented.
/// </remarks>
public static class ProfileFile
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static async Task<ProfileOptions> LoadAsync(
        FileInfo file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var json = await File
            .ReadAllTextAsync(file.FullName, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.Deserialize<ProfileOptions>(json, SerializerOptions)
               ?? throw new InvalidDataException($"Invalid format in {file.FullName}");
    }

    public static Task SaveAsync(
        FileInfo file,
        ProfileOptions profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(profile);

        var json = JsonSerializer.Serialize(profile, SerializerOptions);

        return File.WriteAllTextAsync(file.FullName, json, cancellationToken);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = JsonSerializerOptionsFactory.Create();
        options.PropertyNamingPolicy = null;
        return options;
    }
}