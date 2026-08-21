// filepath: SetVP/Services/SelectionStateService.cs
using SetVP.Models;

namespace SetVP.Services;

/// <summary>
/// 管理当前选择状态与作用域解析
/// </summary>
public static class SelectionStateService
{
    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static AppSettings GetDefaultSettings()
    {
        return new AppSettings
        {
            ViewportScope = "AllLayoutViewports",
            SelectAllReferenceLayers = true,
            ColorMode = "ACI",
            QuickColorIndex = 8,
            CustomColorRgb = "128,128,128"
        };
    }

    /// <summary>
    /// 根据当前文档环境解析实际的作用域
    /// 当前版本返回默认值，实际实现在 ViewportSelectionService 中
    /// </summary>
    public static string ResolveCurrentScope(AppSettings settings)
    {
        // TODO: 在 AutoCAD 文档上下文中查询当前活跃视口
        // 当前原型直接返回已保存的设置
        return settings.ViewportScope;
    }
}
