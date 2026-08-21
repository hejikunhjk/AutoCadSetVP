// filepath: SetVP/Services/LayerSelectionService.cs
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SetVP.Models;

namespace SetVP.Services;

/// <summary>
/// 负责图层枚举与参考图层识别逻辑
/// </summary>
public static class LayerSelectionService
{
    /// <summary>
    /// 获取当前文档中所有满足条件的图层列表
    /// </summary>
    public static List<ReferenceLayerInfo> GetReferenceLayers(AppSettings settings)
    {
        var result = new List<ReferenceLayerInfo>();

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        var db = doc.Database;

        using var tr = db.TransactionManager.StartTransaction();
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

        foreach (ObjectId layerId in lt)
        {
            var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);

            // 跳过匿名图层、参考图层等
            if (string.IsNullOrEmpty(ltr.Name))
                continue;

            // 识别参考（图层名以 xref 前缀 "xref_name|" 开头）
            // 这是 AutoCAD 外部参照图层的标准命名约定
            if (ltr.Name.Contains('|'))
            {
                var parts = ltr.Name.Split('|');
                var xrefName = parts[0];
                var layerName = parts.Length > 1 ? parts[1] : parts[0];

                result.Add(new ReferenceLayerInfo
                {
                    XrefName = xrefName,
                    LayerName = layerName,
                    FullLayerName = ltr.Name,
                    IsSelected = settings.SelectAllReferenceLayers ||
                                 settings.SelectedLayerNames.Contains(ltr.Name)
                });
            }
            else if (settings.SelectAllReferenceLayers)
            {
                // 非 xref 图层也加入，但 IsSelected 为 true 时才处理
                result.Add(new ReferenceLayerInfo
                {
                    XrefName = string.Empty,
                    LayerName = ltr.Name,
                    FullLayerName = ltr.Name,
                    IsSelected = false // 默认不选中非 xref 图层
                });
            }
        }

        tr.Commit();
        return result;
    }

    /// <summary>
    /// 根据设置解析最终要处理的图层名列表
    /// </summary>
    public static List<string> ResolveTargetLayers(AppSettings settings)
    {
        if (settings.SelectAllReferenceLayers)
        {
            // 收集所有 xref 相关图层
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return new List<string>();

            var db = doc.Database;
            var layers = new List<string>();

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            foreach (ObjectId layerId in lt)
            {
                var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                if (ltr.Name.Contains('|'))
                    layers.Add(ltr.Name);
            }

            tr.Commit();
            return layers;
        }

        // 只返回已明确选中的图层
        return settings.SelectedLayerNames.ToList();
    }
}
