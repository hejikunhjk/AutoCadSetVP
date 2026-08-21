// filepath: SetVP/Models/AppSettings.cs
using System.Text.Json.Serialization;

namespace SetVP.Models;

/// <summary>
/// 插件持久化配置数据模型
/// </summary>
public class AppSettings
{
    public int Version { get; set; } = 1;
    public DateTime LastUsedAt { get; set; } = DateTime.Now;

    // 视口作用域: "AllLayoutViewports" | "CurrentViewport" | "SelectedViewports"
    public string ViewportScope { get; set; } = "AllLayoutViewports";

    // 颜色模式: "ByLayer" | "ByBlock" | "ACI" | "CustomRgb"
    public string ColorMode { get; set; } = "ACI";

    // ACI 颜色索引 (0-255)
    public int QuickColorIndex { get; set; } = 8;

    // 自定义 RGB 颜色，格式 "R,G,B"
    public string CustomColorRgb { get; set; } = "128,128,128";

    // 已选中的外部参照名称
    public List<string> SelectedXrefNames { get; set; } = new();

    // 已选中的图层名称
    public List<string> SelectedLayerNames { get; set; } = new();

    // 是否选中所有参考图层
    public bool SelectAllReferenceLayers { get; set; } = true;
}
