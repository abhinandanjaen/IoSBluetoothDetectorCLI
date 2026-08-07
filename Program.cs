using System.Globalization;
using System.Text;
using iPhoneBluetoothDetector;
using Windows.Devices.Bluetooth.Advertisement;

// ---------------------------------------------------------------------------
// iPhone / Apple-device Bluetooth LE detector for Windows
//
// Detects nearby Apple-manufactured Bluetooth Low Energy devices (iPhone, iPad,
// Apple Watch, AirPods, ...) by listening for BLE advertisements that carry
// Apple's manufacturer company id (0x004C).
//
// SECURITY & PRIVACY (Microsoft standards):
//  * Runs with least privilege (no administrator rights required).
//  * Makes NO network connections. Nothing leaves your machine.
//  * Persists NOTHING to disk unless you explicitly pass --export AND consent.
//  * Masks the lower half of device addresses by default.
//  * Only performs passive observation of public broadcast advertisements; it
//    never connects to, pairs with, or reads private data from any device.
// ---------------------------------------------------------------------------

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    CliOptions.PrintHelp();
    return 0;
}

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // No console attached (output redirected); default encoding is fine.
}
PrintBanner();

using var scanner = new BleAppleScanner();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // allow graceful shutdown instead of hard kill
    cts.Cancel();
};

if (options.DurationSeconds > 0)
{
    cts.CancelAfter(TimeSpan.FromSeconds(options.DurationSeconds));
    Console.WriteLine($"Scanning for {options.DurationSeconds} seconds. Press Ctrl+C to stop early.\n");
}
else
{
    Console.WriteLine("Scanning continuously. Press Ctrl+C to stop.\n");
}

try
{
    scanner.Start();
}
catch (Exception ex)
{
    string detail = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
    Console.Error.WriteLine(
        $"Failed to start Bluetooth scanning: {detail} (0x{ex.HResult:X8})");
    Console.Error.WriteLine("Ensure this PC has a Bluetooth adapter and Bluetooth is turned ON,");
    Console.Error.WriteLine("and that this app is allowed to use Bluetooth in Windows privacy settings.");
    return 2;
}

// Live-refresh loop.
try
{
    while (!cts.IsCancellationRequested)
    {
        if (scanner.Status == BluetoothLEAdvertisementWatcherStatus.Aborted)
        {
            Console.Error.WriteLine(
                "\nBluetooth scanning was aborted. Is Bluetooth turned on and is the radio available?");
            return 2;
        }

        RenderTable(scanner, options);
        await Task.Delay(1000, cts.Token).ConfigureAwait(false);
    }
}
catch (OperationCanceledException)
{
    // expected on Ctrl+C or timeout
}

scanner.Stop();
RenderTable(scanner, options);

var finalDevices = scanner.Devices
    .OrderByDescending(d => d.BestRssi)
    .ToList();

Console.WriteLine($"\nScan complete. {finalDevices.Count} distinct Apple device address(es) observed.");
Console.WriteLine("Reminder: iPhones rotate their Bluetooth address ~every 15 min, so counts are not unique people.");

if (options.ExportPath is not null)
{
    if (ConfirmExport(options.ExportPath))
    {
        ExportCsv(finalDevices, options);
        Console.WriteLine($"Exported {finalDevices.Count} record(s) to {options.ExportPath}");
    }
    else
    {
        Console.WriteLine("Export cancelled. No file was written.");
    }
}

return 0;

// ---------------------------------------------------------------------------
// Local helper functions
// ---------------------------------------------------------------------------

static void PrintBanner()
{
    Console.WriteLine("========================================================");
    Console.WriteLine(" Apple / iPhone Bluetooth LE Detector (Windows)");
    Console.WriteLine("========================================================");
    Console.WriteLine("Passively detects nearby Apple BLE devices. No data leaves this PC.");
    Console.WriteLine("Use responsibly and only where you are legally permitted to scan.\n");
}

static void RenderTable(BleAppleScanner scanner, CliOptions options)
{
    var devices = scanner.Devices
        .OrderByDescending(d => d.LatestRssi)
        .ToList();

    var sb = new StringBuilder();
    sb.AppendLine($"[{DateTimeOffset.Now:HH:mm:ss}] Apple devices in range: {devices.Count}");
    sb.AppendLine(new string('-', 96));
    sb.AppendLine(string.Format(
        CultureInfo.InvariantCulture,
        "{0,-20} {1,-34} {2,7} {3,7} {4,8}  {5}",
        "Address", "Inferred kind", "RSSI", "Best", "Seen", "Approx. distance"));
    sb.AppendLine(new string('-', 96));

    foreach (var d in devices)
    {
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0,-20} {1,-34} {2,6}d {3,6}d {4,7}x  {5}",
            d.FormatAddress(mask: !options.ShowFullAddress),
            Truncate(d.InferredKind, 34),
            d.LatestRssi,
            d.BestRssi,
            d.Sightings,
            EstimateProximity(d.LatestRssi)));
    }

    if (Console.IsOutputRedirected)
    {
        // No console buffer to clear (output piped/redirected). Emit incrementally.
        Console.Write(sb.ToString());
        Console.WriteLine();
        return;
    }

    try
    {
        Console.Clear();
    }
    catch (IOException)
    {
        // No interactive console buffer available; fall back to plain output.
    }
    PrintBanner();
    Console.Write(sb.ToString());
    Console.WriteLine("\n(Press Ctrl+C to stop.)");
}

static string EstimateProximity(short rssi) => rssi switch
{
    >= -55 => "Very close (<1 m)",
    >= -67 => "Close (~1-3 m)",
    >= -80 => "Nearby (~3-10 m)",
    _ => "Far (>10 m)",
};

static string Truncate(string value, int max) =>
    value.Length <= max ? value : value[..(max - 1)] + "\u2026";

static bool ConfirmExport(string path)
{
    Console.WriteLine();
    Console.WriteLine("You requested an export to: " + path);
    Console.WriteLine("This writes observed device data to disk. Bluetooth addresses can be");
    Console.WriteLine("personal data under privacy laws (e.g. GDPR). Only proceed if you are");
    Console.WriteLine("authorised to record this information.");
    Console.Write("Type 'yes' to confirm export: ");
    string? answer = Console.ReadLine();
    return string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
}

static void ExportCsv(IEnumerable<DetectedDevice> devices, CliOptions options)
{
    var sb = new StringBuilder();
    sb.AppendLine("address,inferred_kind,latest_rssi_dbm,best_rssi_dbm,sightings,first_seen_utc,last_seen_utc,local_name");
    foreach (var d in devices)
    {
        sb.AppendLine(string.Join(",",
            Csv(d.FormatAddress(mask: !options.ShowFullAddress)),
            Csv(d.InferredKind),
            d.LatestRssi.ToString(CultureInfo.InvariantCulture),
            d.BestRssi.ToString(CultureInfo.InvariantCulture),
            d.Sightings.ToString(CultureInfo.InvariantCulture),
            Csv(d.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(d.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(d.LocalName)));
    }

    File.WriteAllText(options.ExportPath!, sb.ToString(), Encoding.UTF8);
}

// Minimal CSV field escaping to prevent field/formula injection.
static string Csv(string field)
{
    string s = field;
    // Neutralise spreadsheet formula injection, including payloads that hide the
    // trigger character behind leading whitespace (space, tab, CR) which some
    // spreadsheet apps trim before evaluating.
    string trimmed = s.TrimStart(' ', '\t', '\r', '\n');
    if (trimmed.Length > 0 && (trimmed[0] is '=' or '+' or '-' or '@'))
    {
        s = "'" + s;
    }
    if (s.Contains(',', StringComparison.Ordinal) ||
        s.Contains('"', StringComparison.Ordinal) ||
        s.Contains('\n', StringComparison.Ordinal))
    {
        s = "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
    return s;
}
