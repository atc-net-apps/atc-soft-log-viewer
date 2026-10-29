# Atc.LogViewer.SampleGenerator

Generates large log files with simulated application logging, for testing how the log viewer handles big files.

Entries use mixed log levels; errors carry a stack trace and some entries a JSON payload, so about one in ten lines belongs to a multi-line entry. The formats match the layouts the log viewer reads.

## Usage

```bash
# 100 million NLog lines (~7.7 GB) - generated sample/*.log files are ignored by git
dotnet run --project tool/Atc.LogViewer.SampleGenerator -c Release -- --format nlog --lines 100_000_000 --output sample/sample-nlog-big.log

# 1 million log4net lines in the current directory (sample-log4net-1000000.log)
dotnet run --project tool/Atc.LogViewer.SampleGenerator -c Release -- --format log4net
```

| Option | Default | Description |
|---|---|---|
| `-f`, `--format` | `nlog` | `nlog`, `log4net` or `serilog` |
| `-l`, `--lines` | `1000000` | Exact number of lines; underscores are allowed (`100_000_000`) |
| `-o`, `--output` | `sample-<format>-<lines>.log` | File to write (overwritten) |
| `--seed` | `42` | Same seed gives the same content |
