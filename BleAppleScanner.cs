using System.Collections.Concurrent;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace iPhoneBluetoothDetector;

/// <summary>
/// Scans for nearby Bluetooth Low Energy devices manufactured by Apple and
/// classifies them by the "Continuity" message type embedded in Apple's
/// manufacturer-specific advertisement data.
/// </summary>
public sealed class BleAppleScanner : IDisposable
{
    /// <summary>Apple, Inc. Bluetooth SIG company identifier.</summary>
    public const ushort AppleCompanyId = 0x004C;

    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly ConcurrentDictionary<ulong, DetectedDevice> _devices = new();
    private bool _disposed;

    /// <summary>Raised whenever an Apple device is seen (new or updated).</summary>
    public event EventHandler<DetectedDevice>? DeviceObserved;

    public BleAppleScanner()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            // Active scanning requests scan-response data, improving classification.
            ScanningMode = BluetoothLEScanningMode.Active,
        };

        // Only surface advertisements that actually contain Apple manufacturer data.
        _watcher.AdvertisementFilter.Advertisement.ManufacturerData.Add(
            new BluetoothLEManufacturerData(AppleCompanyId, new DataWriter().DetachBuffer()));

        _watcher.Received += OnAdvertisementReceived;
    }

    /// <summary>Snapshot of all Apple devices observed so far.</summary>
    public IReadOnlyCollection<DetectedDevice> Devices => _devices.Values.ToArray();

    public BluetoothLEAdvertisementWatcherStatus Status => _watcher.Status;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _watcher.Start();
    }

    public void Stop()
    {
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            _watcher.Stop();
        }
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var appleData = args.Advertisement.ManufacturerData
            .FirstOrDefault(m => m.CompanyId == AppleCompanyId);

        // Defense in depth: the filter should guarantee this, but never trust input.
        if (appleData is null)
        {
            return;
        }

        string kind = ClassifyApplePayload(appleData.Data);
        string localName = args.Advertisement.LocalName ?? string.Empty;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DetectedDevice device = _devices.AddOrUpdate(
            args.BluetoothAddress,
            addr =>
            {
                var d = new DetectedDevice(addr, now)
                {
                    LatestRssi = args.RawSignalStrengthInDBm,
                    BestRssi = args.RawSignalStrengthInDBm,
                    InferredKind = kind,
                    LocalName = localName,
                    Sightings = 1,
                };
                return d;
            },
            (addr, existing) =>
            {
                existing.LastSeenUtc = now;
                existing.LatestRssi = args.RawSignalStrengthInDBm;
                if (args.RawSignalStrengthInDBm > existing.BestRssi)
                {
                    existing.BestRssi = args.RawSignalStrengthInDBm;
                }
                existing.Sightings++;
                if (!string.IsNullOrEmpty(localName))
                {
                    existing.LocalName = localName;
                }
                // Prefer a more specific classification if we learn one.
                if (kind != "Apple device")
                {
                    existing.InferredKind = kind;
                }
                return existing;
            });

        DeviceObserved?.Invoke(this, device);
    }

    /// <summary>
    /// Maps the first byte of Apple's manufacturer payload (the "Continuity"
    /// message type) to a friendly Apple product family. This is a best-effort
    /// heuristic based on publicly documented Apple Continuity message types.
    /// </summary>
    private static string ClassifyApplePayload(IBuffer buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return "Apple device";
        }

        using var reader = DataReader.FromBuffer(buffer);
        byte messageType = reader.ReadByte();

        return messageType switch
        {
            0x02 => "iBeacon",
            0x05 => "AirDrop",
            0x07 => "AirPods / Proximity Pairing",
            0x08 => "\"Hey Siri\" device",
            0x09 => "AirPlay target",
            0x0A => "AirPlay source",
            0x0B => "Apple Watch magic switch",
            0x0C => "Handoff (iPhone/iPad/Mac)",
            0x0D => "Tethering / Personal Hotspot source",
            0x0E => "Nearby Action (iPhone/iPad)",
            0x0F => "Nearby Info (likely iPhone/iPad)",
            0x10 => "Nearby (likely iPhone/iPad)",
            0x12 => "Find My",
            _ => "Apple device",
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watcher.Received -= OnAdvertisementReceived;
        Stop();
        _disposed = true;
    }
}
