using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DKLogoEditor.Models;

namespace DKLogoEditor.Storage;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DKLogoEditor");

        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_settingsPath);
        var stored = JsonSerializer.Deserialize<StoredSettings>(json, JsonOptions) ?? new StoredSettings();

        return new AppSettings
        {
            ApiKey = Decrypt(stored.EncryptedApiKey),
            DefaultModelId = string.IsNullOrWhiteSpace(stored.DefaultModelId)
                ? ModelPresets.DefaultModelId
                : stored.DefaultModelId,
            CustomModels = stored.CustomModels ?? []
        };
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);

        var stored = new StoredSettings
        {
            EncryptedApiKey = Encrypt(settings.ApiKey),
            DefaultModelId = settings.DefaultModelId,
            CustomModels = settings.CustomModels
        };

        var json = JsonSerializer.Serialize(stored, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static string Encrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(value);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    private static string Decrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var encryptedBytes = Convert.FromBase64String(value);
        var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private sealed class StoredSettings
    {
        public string EncryptedApiKey { get; set; } = string.Empty;

        public string DefaultModelId { get; set; } = ModelPresets.DefaultModelId;

        public List<string>? CustomModels { get; set; }
    }
}
