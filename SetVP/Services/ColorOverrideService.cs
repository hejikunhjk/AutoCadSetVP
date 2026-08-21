// filepath: SetVP/Services/ColorOverrideService.cs
using System.Reflection;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace SetVP.Services;

/// <summary>
/// 负责视口图层替代（Viewport Layer Overrides）的备份、应用与恢复。
/// 
/// 关键发现（AutoCAD 2026）：
/// - 直接设置 LayerViewportProperties.Color + IsColorOverridden = true 无效
/// - IsColorOverridden = true 的 setter 会把 Color 重置为图层原色
/// - 必须使用 private SetColorOverride(Color color, ObjectId viewportId) 反射方法
/// - 恢复使用 RemoveAllOverrides()
/// </summary>
public static class ColorOverrideService
{
    /// <summary>
    /// 记录每个 (视口, 图层) 组合是否被我们修改过（用于还原）
    /// </summary>
    private static HashSet<(ObjectId vpId, string layerName)> s_overridden = new();

    /// <summary>
    /// private SetColorOverride 方法的反射缓存
    /// </summary>
    private static MethodInfo? s_setColorOverrideMethod;

    /// <summary>
    /// private RemoveColorOverride 方法的反射缓存
    /// </summary>
    private static MethodInfo? s_removeColorOverrideMethod;

    /// <summary>
    /// 获取 private SetColorOverride 反射方法
    /// </summary>
    private static MethodInfo GetSetColorOverrideMethod()
    {
        if (s_setColorOverrideMethod == null)
        {
            s_setColorOverrideMethod = typeof(LayerTableRecord).GetMethod(
                "SetColorOverride",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
        return s_setColorOverrideMethod!;
    }

    /// <summary>
    /// 获取 private RemoveColorOverride 反射方法
    /// </summary>
    private static MethodInfo GetRemoveColorOverrideMethod()
    {
        if (s_removeColorOverrideMethod == null)
        {
            s_removeColorOverrideMethod = typeof(LayerTableRecord).GetMethod(
                "RemoveColorOverride",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
        return s_removeColorOverrideMethod!;
    }

    /// <summary>
    /// 解析快速颜色（ACI 索引 -> AutoCAD Color）
    /// </summary>
    public static AcadColor IndexToAcadColor(int aciIndex)
    {
        return AcadColor.FromColorIndex(ColorMethod.ByAci, (short)aciIndex);
    }

    /// <summary>
    /// 解析自定义 RGB 字符串到 AutoCAD Color
    /// </summary>
    public static AcadColor RgbToAcadColor(string rgb)
    {
        var parts = rgb.Split(',');
        if (parts.Length != 3) return IndexToAcadColor(8);

        var r = byte.TryParse(parts[0], out var rVal) ? rVal : (byte)128;
        var g = byte.TryParse(parts[1], out var gVal) ? gVal : (byte)128;
        var b = byte.TryParse(parts[2], out var bVal) ? bVal : (byte)128;

        return AcadColor.FromRgb(r, g, b);
    }

    /// <summary>
    /// 将特殊颜色方法转换成 AutoCAD 可接受的实际颜色值。
    /// AutoCAD 的 LayerTableRecord.SetColorOverride 不接受 ByLayer / ByBlock 这类特殊方法值，
    /// 必须转换为实际 RGB/ACI 颜色后再调用。
    /// </summary>
    private static AcadColor ResolveValidOverrideColor(LayerTableRecord layerRecord, AcadColor requestedColor)
    {
        if (requestedColor.ColorMethod == ColorMethod.ByLayer || requestedColor.ColorMethod == ColorMethod.ByBlock)
        {
            var fallback = layerRecord.Color;
            if (fallback != null && fallback.ColorMethod != ColorMethod.ByLayer && fallback.ColorMethod != ColorMethod.ByBlock)
            {
                return fallback;
            }

            // 兜底：如果当前图层颜色本身也是 ByLayer/ByBlock，则使用一个合法的默认颜色。
            return AcadColor.FromColorIndex(ColorMethod.ByAci, 7);
        }

        return requestedColor;
    }

    /// <summary>
    /// 将颜色覆盖应用到指定视口列表的指定图层
    /// 使用 private SetColorOverride(Color, ObjectId) 反射方法
    /// </summary>
    public static void ApplyColorOverride(
        IEnumerable<ObjectId> viewportIds,
        IEnumerable<string> layerNames,
        AcadColor newColor)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var db = doc.Database;

        var viewportList = viewportIds.ToList();
        var layerList = layerNames.ToList();

        // 预取所有图层 ID（只读事务）
        var layerIds = new Dictionary<string, ObjectId>();
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var name in layerList)
            {
                if (!lt.Has(name)) continue;
                layerIds[name] = lt[name];
            }
            tr.Commit();
        }

        var setColorMethod = GetSetColorOverrideMethod();

        // 锁定文档后逐 (视口, 图层) 应用
        using (doc.LockDocument())
        {
            foreach (var vpId in viewportList)
            {
                foreach (var layerName in layerList)
                {
                    if (!layerIds.TryGetValue(layerName, out var layerId))
                        continue;

                    using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);

                        // 标记为已修改（用于还原）
                        s_overridden.Add((vpId, layerName));

                        var effectiveColor = ResolveValidOverrideColor(ltr, newColor);

                        // 使用 private SetColorOverride 方法设置颜色
                        // 参数顺序: (Color color, ObjectId viewportId)
                        setColorMethod.Invoke(ltr, new object[] { effectiveColor, vpId });

                        tr.Commit();
                    }
                }
            }
        }

        doc.Editor.Regen();
    }

    /// <summary>
    /// 恢复所有被修改过的视口图层替代
    /// </summary>
    public static void RestoreOriginalColors()
    {
        if (s_overridden.Count == 0) return;

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var db = doc.Database;

        using (doc.LockDocument())
        {
            // 复制一份，因为清除过程会修改集合
            var toRestore = s_overridden.ToList();

            foreach (var (vpId, layerName) in toRestore)
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(layerName)) continue;
                    var layerId = lt[layerName];

                    var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);

                    // 使用 RemoveAllOverrides 清除所有视口覆盖
                    ltr.RemoveAllOverrides();

                    tr.Commit();
                }

                s_overridden.Remove((vpId, layerName));
            }
        }

        doc.Editor.Regen();
    }

    /// <summary>
    /// 判断是否有待还原的替代
    /// </summary>
    public static bool HasOverrides() => s_overridden.Count > 0;

    /// <summary>
    /// 对指定视口和图层，应用 ByLayer 颜色。
    /// 
    /// AutoCAD Color System:
    /// - ByLayer (62=256): 移除视口覆盖，让图层恢复为 ByLayer（随图层颜色）
    /// - 使用 RemoveColorOverride 移除该图层的视口颜色覆盖
    /// </summary>
    public static void ApplyColorOverrideByLayerColor(
        IEnumerable<ObjectId> viewportIds,
        IEnumerable<string> layerNames)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var db = doc.Database;

        var viewportList = viewportIds.ToList();
        var layerList = layerNames.ToList();

        // 预取所有图层 ID（只读事务）
        var layerIds = new Dictionary<string, ObjectId>();
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var name in layerList)
            {
                if (!lt.Has(name)) continue;
                layerIds[name] = lt[name];
            }
            tr.Commit();
        }

        var removeColorMethod = GetRemoveColorOverrideMethod();

        using (doc.LockDocument())
        {
            foreach (var vpId in viewportList)
            {
                foreach (var layerName in layerList)
                {
                    if (!layerIds.TryGetValue(layerName, out var layerId))
                        continue;

                    using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);

                        s_overridden.Add((vpId, layerName));

                        // ByLayer: 移除视口覆盖，让图层恢复为 ByLayer（随图层颜色）
                        // 调用 RemoveColorOverride(ObjectId viewportId)
                        removeColorMethod.Invoke(ltr, new object[] { vpId });

                        tr.Commit();
                    }
                }
            }
        }

        doc.Editor.Regen();
    }

    /// <summary>
    /// 对指定视口和图层，应用 ByBlock 颜色。
    /// 
    /// AutoCAD Color System:
    /// - ByBlock (62=0): 移除视口覆盖，让图层恢复为 ByBlock（随块颜色）
    /// - 使用 RemoveColorOverride 移除该图层的视口颜色覆盖
    /// </summary>
    public static void ApplyColorOverrideByBlockColor(
        IEnumerable<ObjectId> viewportIds,
        IEnumerable<string> layerNames)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var db = doc.Database;

        var viewportList = viewportIds.ToList();
        var layerList = layerNames.ToList();

        // 预取所有图层 ID（只读事务）
        var layerIds = new Dictionary<string, ObjectId>();
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var name in layerList)
            {
                if (!lt.Has(name)) continue;
                layerIds[name] = lt[name];
            }
            tr.Commit();
        }

        var removeColorMethod = GetRemoveColorOverrideMethod();

        using (doc.LockDocument())
        {
            foreach (var vpId in viewportList)
            {
                foreach (var layerName in layerList)
                {
                    if (!layerIds.TryGetValue(layerName, out var layerId))
                        continue;

                    using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);

                        s_overridden.Add((vpId, layerName));

                        // ByBlock: 移除视口覆盖，让图层恢复为 ByBlock（随块颜色）
                        // 调用 RemoveColorOverride(ObjectId viewportId)
                        removeColorMethod.Invoke(ltr, new object[] { vpId });

                        tr.Commit();
                    }
                }
            }
        }

        doc.Editor.Regen();
    }
}
