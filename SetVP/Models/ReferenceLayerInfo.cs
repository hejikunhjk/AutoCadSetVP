// filepath: SetVP/Models/ReferenceLayerInfo.cs
namespace SetVP.Models;

/// <summary>
/// 参考图层信息（用于 UI 展示和选择）
/// </summary>
public class ReferenceLayerInfo
{
    /// <summary>所属外部参照名称</summary>
    public string XrefName { get; set; } = string.Empty;

    /// <summary>图层名称（不含 xref 前缀）</summary>
    public string LayerName { get; set; } = string.Empty;

    /// <summary>完整图层名称（包含 xref 前缀）</summary>
    public string FullLayerName { get; set; } = string.Empty;

    /// <summary>是否在 UI 中被选中</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>该 xref 包含的所有图层名</summary>
    public List<string> XrefMembers { get; set; } = new();
}
