using System;
using System.Buffers;
using System.IO;
using System.Text.Json;

namespace TDS.PeekDesktop;

/// <summary>
/// Persisted Peek Desktop options (integrated into TDS; startup is handled by main app Run key).
/// </summary>
public sealed class PeekDesktopSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TDS");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "peekdesktop.json");

    private static readonly object SettingsIoLock = new();
    private static long _cachedFileWriteTicks;
    private static PeekDesktopSettings? _cachedSnapshot;

    public bool Enabled { get; set; } = true;
    public bool RequireDoubleClick { get; set; } = false;
    public bool PauseWhileFullscreenAppActive { get; set; } = true;
    public bool PeekOnTaskbarClick { get; set; } = false;
    public PeekMode PeekMode { get; set; } = PeekMode.NativeShowDesktop;

    public static PeekDesktopSettings Load()
    {
        lock (SettingsIoLock)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    long writeTicks = File.GetLastWriteTimeUtc(SettingsPath).Ticks;
                    if (_cachedSnapshot is not null && writeTicks == _cachedFileWriteTicks)
                        return _cachedSnapshot.Clone();

                    byte[] jsonBytes = File.ReadAllBytes(SettingsPath);
                    var settings = DeserializeUtf8(jsonBytes);
                    PeekMode normalizedMode = NormalizePeekMode(settings.PeekMode);
                    if (settings.PeekMode != normalizedMode)
                    {
                        settings.PeekMode = normalizedMode;
                        settings.SaveCoreUnlocked();
                    }

                    _cachedFileWriteTicks = File.GetLastWriteTimeUtc(SettingsPath).Ticks;
                    _cachedSnapshot = settings;
                    return settings.Clone();
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log($"Failed to load peek settings from {SettingsPath}: {ex.Message}");
            }

            _cachedSnapshot = null;
            _cachedFileWriteTicks = 0;
            return new PeekDesktopSettings();
        }
    }

    public PeekDesktopSettings Clone() => new()
    {
        Enabled = Enabled,
        RequireDoubleClick = RequireDoubleClick,
        PauseWhileFullscreenAppActive = PauseWhileFullscreenAppActive,
        PeekOnTaskbarClick = PeekOnTaskbarClick,
        PeekMode = PeekMode
    };

    public void Save()
    {
        lock (SettingsIoLock)
            SaveCoreUnlocked();
    }

    private void SaveCoreUnlocked()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            byte[] jsonBytes = SerializeUtf8();
            File.WriteAllBytes(SettingsPath, jsonBytes);
            _cachedFileWriteTicks = File.GetLastWriteTimeUtc(SettingsPath).Ticks;
            _cachedSnapshot = Clone();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log($"Failed to save peek settings to {SettingsPath}: {ex.Message}");
        }
    }

    private static PeekMode NormalizePeekMode(PeekMode peekMode)
    {
        return peekMode switch
        {
            PeekMode.FlyAway => PeekMode.FlyAway,
            PeekMode.NativeShowDesktop => PeekMode.NativeShowDesktop,
            PeekMode.Minimize => PeekMode.Minimize,
            _ => PeekMode.NativeShowDesktop
        };
    }

    private static PeekDesktopSettings DeserializeUtf8(ReadOnlySpan<byte> utf8Json)
    {
        var settings = new PeekDesktopSettings();
        var reader = new Utf8JsonReader(utf8Json);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return settings;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (reader.ValueTextEquals("Enabled"u8))
            {
                reader.Read();
                settings.Enabled = reader.GetBoolean();
            }
            else if (reader.ValueTextEquals("RequireDoubleClick"u8))
            {
                reader.Read();
                settings.RequireDoubleClick = reader.GetBoolean();
            }
            else if (reader.ValueTextEquals("PauseWhileFullscreenAppActive"u8))
            {
                reader.Read();
                settings.PauseWhileFullscreenAppActive = reader.GetBoolean();
            }
            else if (reader.ValueTextEquals("PeekOnTaskbarClick"u8))
            {
                reader.Read();
                settings.PeekOnTaskbarClick = reader.GetBoolean();
            }
            else if (reader.ValueTextEquals("PeekMode"u8))
            {
                reader.Read();
                settings.PeekMode = (PeekMode)reader.GetInt32();
            }
            else
            {
                reader.Skip();
            }
        }

        return settings;
    }

    private byte[] SerializeUtf8()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteBoolean("Enabled"u8, Enabled);
        writer.WriteBoolean("RequireDoubleClick"u8, RequireDoubleClick);
        writer.WriteBoolean("PauseWhileFullscreenAppActive"u8, PauseWhileFullscreenAppActive);
        writer.WriteBoolean("PeekOnTaskbarClick"u8, PeekOnTaskbarClick);
        writer.WriteNumber("PeekMode"u8, (int)PeekMode);
        writer.WriteEndObject();

        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
