using System.Globalization;

namespace iPhoneBluetoothDetector;

/// <summary>
/// Parses and validates command-line arguments. All parsing is defensive:
/// unknown or malformed input results in help output rather than a crash.
/// </summary>
public sealed class CliOptions
{
    public bool ShowHelp { get; private set; }
    public int DurationSeconds { get; private set; }
    public bool ShowFullAddress { get; private set; }
    public string? ExportPath { get; private set; }

    private CliOptions() { }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help" or "/?":
                    options.ShowHelp = true;
                    break;

                case "-s" or "--seconds":
                    options.DurationSeconds = ParseDuration(NextValue(args, ref i));
                    break;

                case "--show-full-address":
                    options.ShowFullAddress = true;
                    break;

                case "--export":
                    options.ExportPath = ValidateExportPath(NextValue(args, ref i));
                    break;

                default:
                    Console.Error.WriteLine($"Unknown argument: {arg}");
                    options.ShowHelp = true;
                    break;
            }
        }

        return options;
    }

    private static string? NextValue(string[] args, ref int i)
    {
        if (i + 1 < args.Length)
        {
            i++;
            return args[i];
        }
        return null;
    }

    private static int ParseDuration(string? value)
    {
        if (value is not null &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) &&
            seconds is > 0 and <= 86_400)
        {
            return seconds;
        }

        Console.Error.WriteLine("--seconds requires an integer between 1 and 86400. Ignoring.");
        return 0;
    }

    private static string? ValidateExportPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Console.Error.WriteLine("--export requires a file path. Ignoring.");
            return null;
        }

        // Reject paths containing invalid characters; resolve to a full path so
        // we never write to an unexpected relative location silently.
        if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            Console.Error.WriteLine("--export path contains invalid characters. Ignoring.");
            return null;
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine("--export path is invalid: " + ex.Message + ". Ignoring.");
            return null;
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            Apple / iPhone Bluetooth LE Detector for Windows

            USAGE:
              iPhoneBluetoothDetector [options]

            OPTIONS:
              -s, --seconds <N>       Scan for N seconds (1-86400), then stop.
                                      Default: scan until Ctrl+C.
              --show-full-address     Show the full Bluetooth address instead of the
                                      privacy-masked form. Use only when authorised.
              --export <path>         After scanning, export results to a CSV file.
                                      Requires interactive 'yes' confirmation.
              -h, --help              Show this help.

            NOTES:
              * Requires a Bluetooth adapter with Bluetooth turned ON.
              * Detects Apple devices via Apple's BLE manufacturer id (0x004C).
              * iPhones randomise their Bluetooth address (~15 min), so this cannot
                persistently identify a specific person.
              * Makes no network connections; nothing leaves your PC unless you export.
            """);
    }
}
