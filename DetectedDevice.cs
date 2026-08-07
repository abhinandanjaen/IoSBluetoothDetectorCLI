using System.Globalization;

namespace iPhoneBluetoothDetector;

/// <summary>
/// Represents an Apple-manufactured Bluetooth Low Energy device observed nearby.
///
/// NOTE ON PRIVACY: Apple devices (iPhone, iPad, Watch, AirPods, ...) rotate a
/// random, non-resolvable Bluetooth address roughly every 15 minutes. The
/// <see cref="Address"/> below is therefore an ephemeral identifier and cannot be
/// used to persistently track a specific individual across time.
/// </summary>
public sealed class DetectedDevice
{
    /// <summary>The (ephemeral, randomized) 48-bit Bluetooth address.</summary>
    public ulong Address { get; }

    /// <summary>Human-readable Apple product family inferred from Apple's manufacturer data.</summary>
    public string InferredKind { get; internal set; }

    /// <summary>Most recent received signal strength in dBm (closer to 0 = nearer).</summary>
    public short LatestRssi { get; internal set; }

    /// <summary>Strongest signal ever seen for this device (rough "closest approach").</summary>
    public short BestRssi { get; internal set; }

    /// <summary>Local advertised name, if any was broadcast (often empty for iPhones).</summary>
    public string LocalName { get; internal set; }

    public DateTimeOffset FirstSeenUtc { get; }
    public DateTimeOffset LastSeenUtc { get; internal set; }

    /// <summary>Number of advertisement packets received from this address.</summary>
    public long Sightings { get; internal set; }

    public DetectedDevice(ulong address, DateTimeOffset firstSeenUtc)
    {
        Address = address;
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = firstSeenUtc;
        InferredKind = "Apple device";
        LocalName = string.Empty;
        Sightings = 0;
    }

    /// <summary>
    /// Formats the address as a colon-separated MAC string. When <paramref name="mask"/>
    /// is true (the default), the lower 24 bits are redacted to reduce the chance of
    /// logging a fully identifying address to disk/console.
    /// </summary>
    public string FormatAddress(bool mask = true)
    {
        var bytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            bytes[i] = (byte)((Address >> (8 * (5 - i))) & 0xFF);
        }

        if (mask)
        {
            bytes[3] = 0;
            bytes[4] = 0;
            bytes[5] = 0;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:X2}:{1:X2}:{2:X2}:XX:XX:XX",
                bytes[0], bytes[1], bytes[2]);
        }

        return string.Join(":", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
