// filepath: SetVP/Services/SettingsManager.cs
using System.IO;
using System.Text.Json;
using SetVP.Models;

namespace SetVP.Services;

/// <summary>
/// 负责插件配置的加载与保存，全局单例
/// </summary>
public static class SettingsManager
{
    private static readonly string AppDataFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SetVP");

    private static readonly string SettingsFilePath =
        Path.Combine(AppDataFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 加载配置。如果文件不存在或损坏，返回 null
    /// </summary>
    public static AppSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return null;

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 保存配置到 AppData\Roaming\SetVP\settings.json
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);

            settings.LastUsedAt = DateTime.Now;
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // 静默失败，不影响主流程
        }
    }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static AppSettings GetDefault()
    {
        return new AppSettings();
    }
}
