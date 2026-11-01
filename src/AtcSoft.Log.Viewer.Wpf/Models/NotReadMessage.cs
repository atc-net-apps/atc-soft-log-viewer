namespace AtcSoft.Log.Viewer.Wpf.Models;

/// <summary>
/// What the status bar shows about lines and files that were not read.
/// </summary>
/// <param name="ShortText">For example "Not read: 5,810 lines"; empty when everything was read.</param>
/// <param name="Details">The files and example lines, for the tooltip.</param>
public sealed record NotReadMessage(
    string ShortText,
    string Details);