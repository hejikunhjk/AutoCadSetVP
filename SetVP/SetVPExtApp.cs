// filepath: SetVP/SetVPExtApp.cs
using Autodesk.AutoCAD.Runtime;

namespace SetVP;

/// <summary>
/// SetVP 插件扩展应用接口实现
/// 用于加载/卸载时的初始化和清理
/// </summary>
public class SetVPExtApp : IExtensionApplication
{
    public void Initialize()
    {
        // 插件加载时的初始化
        // 命令通过 [CommandMethod] 属性自动注册
    }

    public void Terminate()
    {
        // 插件卸载时的清理
        // 关闭打开的窗口等资源
        Services.ColorOverrideService.RestoreOriginalColors();
        UI.SetVpForm.CloseIfOpen();
    }
}
