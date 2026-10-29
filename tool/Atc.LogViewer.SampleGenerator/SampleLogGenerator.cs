namespace Atc.LogViewer.SampleGenerator;

/// <summary>
/// Writes a log file with simulated application logging: mixed levels, stack traces on errors
/// and the occasional JSON payload, so some entries span several lines.
/// </summary>
public static class SampleLogGenerator
{
    private const int StackTraceLineCount = 4;
    private const int JsonPayloadLineCount = 5;

    private static readonly string[] Customers = ["Contoso", "Fabrikam", "Northwind", "Adventure Works", "Tailspin Toys", "Wide World Importers"];
    private static readonly string[] Services = ["OrderService", "PaymentService", "InventoryService", "ShippingService", "CustomerService"];

    private static readonly CompositeFormat[] DebugMessages =
    [
        CompositeFormat.Parse("Cache lookup for key order:{0} returned miss"),
        CompositeFormat.Parse("Opening database connection to sql-prod-01 (pool size {1})"),
        CompositeFormat.Parse("Validating order {0} with {1} lines"),
        CompositeFormat.Parse("Serializing response for order {0}"),
    ];

    private static readonly CompositeFormat[] InformationMessages =
    [
        CompositeFormat.Parse("Order {0} processed for customer {2} in {1} ms"),
        CompositeFormat.Parse("Payment authorized for order {0} ({1}.00 EUR)"),
        CompositeFormat.Parse("Shipment created for order {0} with {1} parcels"),
        CompositeFormat.Parse("User {2} signed in from 10.0.{1}.17"),
        CompositeFormat.Parse("Inventory reserved for order {0}: {1} items"),
    ];

    private static readonly CompositeFormat[] WarningMessages =
    [
        CompositeFormat.Parse("Slow response from {3}: {1} ms for order {0}"),
        CompositeFormat.Parse("Retrying call to {3} for order {0} (attempt {1})"),
        CompositeFormat.Parse("Inventory low for item SKU-{1} while processing order {0}"),
    ];

    private static readonly CompositeFormat[] ErrorMessages =
    [
        CompositeFormat.Parse("Failed to process order {0} for customer {2}"),
        CompositeFormat.Parse("Payment declined for order {0}"),
        CompositeFormat.Parse("Timeout calling {3} for order {0}"),
    ];

    /// <summary>
    /// Generates the file.
    /// </summary>
    /// <param name="options">What to generate.</param>
    /// <param name="onProgress">Optionally called with the number of lines written, about every 10 million lines.</param>
    /// <returns>What was generated.</returns>
    public static GeneratorResult Generate(
        GeneratorOptions options,
        Action<long>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.LineCount <= 0)
        {
            throw new ArgumentException("The number of lines must be positive.", nameof(options));
        }

        var stopwatch = Stopwatch.StartNew();
        var random = new Random(options.Seed);
        var timeStamp = new DateTime(2024, 3, 26, 0, 0, 0, DateTimeKind.Unspecified);
        var linesWritten = 0L;
        var entryCount = 0L;
        var nextProgress = 10_000_000L;

        var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var writer = new StreamWriter(
                   options.OutputPath,
                   append: false,
                   new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                   bufferSize: 1 << 20))
        {
            writer.NewLine = "\r\n";
            var line = new StringBuilder(256);

            while (linesWritten < options.LineCount)
            {
                entryCount++;
                timeStamp = timeStamp.AddTicks(random.Next(1, 20_000) * TimeSpan.TicksPerMillisecond / 10);

                var orderId = 100_000 + (entryCount % 900_000);
                var number = random.Next(2, 950);
                var customer = Customers[random.Next(Customers.Length)];
                var service = Services[random.Next(Services.Length)];

                var roll = random.Next(100);
                var (level, templates) = roll switch
                {
                    < 10 => (SampleLevel.Trace, DebugMessages),
                    < 35 => (SampleLevel.Debug, DebugMessages),
                    < 85 => (SampleLevel.Information, InformationMessages),
                    < 96 => (SampleLevel.Warning, WarningMessages),
                    < 99 => (SampleLevel.Error, ErrorMessages),
                    _ => (SampleLevel.Critical, ErrorMessages),
                };

                line.Clear();
                RootLineWriter.Append(line, options.Format, timeStamp, level, $"Contoso.Orders.{service}", threadId: (number % 16) + 1);
                line.AppendFormat(CultureInfo.InvariantCulture, templates[random.Next(templates.Length)], orderId, number, customer, service);
                writer.WriteLine(line);
                linesWritten++;

                var remaining = options.LineCount - linesWritten;
                if (level >= SampleLevel.Error && remaining >= StackTraceLineCount)
                {
                    WriteStackTrace(writer, orderId, service, number);
                    linesWritten += StackTraceLineCount;
                }
                else if (roll == 50 && remaining >= JsonPayloadLineCount)
                {
                    WriteJsonPayload(writer, orderId, customer, number);
                    linesWritten += JsonPayloadLineCount;
                }

                if (onProgress is not null &&
                    linesWritten >= nextProgress)
                {
                    onProgress(linesWritten);
                    nextProgress += 10_000_000L;
                }
            }
        }

        return new GeneratorResult(
            linesWritten,
            entryCount,
            new FileInfo(options.OutputPath).Length,
            stopwatch.Elapsed);
    }

    private static void WriteStackTrace(
        StreamWriter writer,
        long orderId,
        string service,
        int lineNumber)
    {
        writer.Write("System.InvalidOperationException: Order ");
        writer.Write(orderId);
        writer.Write(" could not be processed by ");
        writer.WriteLine(service);
        writer.Write("   at Contoso.Orders.");
        writer.Write(service);
        writer.Write(".ProcessAsync(Order order, CancellationToken cancellationToken) in ");
        writer.Write(service);
        writer.Write(".cs:line ");
        writer.WriteLine(lineNumber);
        writer.WriteLine("   at Contoso.Orders.OrderWorker.ExecuteAsync(CancellationToken stoppingToken) in OrderWorker.cs:line 87");
        writer.WriteLine("   at Microsoft.Extensions.Hosting.Internal.Host.TryExecuteBackgroundServiceAsync(BackgroundService backgroundService)");
    }

    private static void WriteJsonPayload(
        StreamWriter writer,
        long orderId,
        string customer,
        int lines)
    {
        writer.WriteLine("{");
        writer.Write("  \"orderId\": ");
        writer.Write(orderId);
        writer.WriteLine(",");
        writer.Write("  \"customer\": \"");
        writer.Write(customer);
        writer.WriteLine("\",");
        writer.Write("  \"lines\": ");
        writer.WriteLine(lines);
        writer.WriteLine("}");
    }
}