namespace Atc.LogFiles.Simulator;

/// <summary>
/// How a log file is rolled when it is full, or when a roll is requested.
/// </summary>
public enum RollMode
{
    /// <summary>
    /// Renames <c>orders.log</c> to <c>orders.log.1</c> (older ones shift up) and starts a new <c>orders.log</c>, like log4net.
    /// </summary>
    Rename,

    /// <summary>
    /// Starts a new file with a time stamp in its name, such as <c>orders-20261029-153012.log</c>, like Serilog and NLog archives.
    /// </summary>
    NewFile,

    /// <summary>
    /// Copies <c>orders.log</c> to <c>orders.log.20261029-153012</c> and truncates it, like logrotate's copytruncate.
    /// </summary>
    CopyTruncate,
}