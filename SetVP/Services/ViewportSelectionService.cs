// filepath: SetVP/Services/ViewportSelectionService.cs
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SetVP.Services;

/// <summary>
/// 负责视口选择逻辑，封装 AutoCAD API 调用
/// </summary>
public static class ViewportSelectionService
{
    /// <summary>
    /// 获取所有布局视口窗口的 ObjectId 列表（遍历所有 Layout）
    /// 重要：Number=1 是布局本身的视口，不是视口窗口，会被过滤掉
    /// </summary>
    public static List<ObjectId> GetAllLayoutViewports()
    {
        var result = new List<ObjectId>();

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        var db = doc.Database;

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        // 遍历所有 BlockTableRecord（ModelSpace、PaperSpace 及各 Layout）
        foreach (ObjectId btrId in bt)
        {
            // 跳过 ModelSpace（只处理 PaperSpace/Layout）
            if (btrId == bt[BlockTableRecord.ModelSpace]) continue;

            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.Name == "AcDbViewport")
                {
                    // 过滤掉 Number=1 的视口（它是布局本身的视口）
                    var vp = (Viewport)tr.GetObject(id, OpenMode.ForRead);
                    if (vp.Number > 1)
                    {
                        result.Add(id);
                    }
                }
            }
        }

        tr.Commit();
        return result;
    }

    /// <summary>
    /// 获取当前活动视口的 ObjectId
    /// 注意：CurrentViewportObjectId 返回的是布局本身的视口(Number=1)
    /// 如果需要布局内的视口窗口，应该使用 GetAllLayoutViewports()
    /// </summary>
    public static ObjectId? GetCurrentViewport()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return null;

        // 获取当前文档窗口的视口（这会返回 Number=1 的布局视口）
        var viewportId = doc.Editor.CurrentViewportObjectId;
        if (viewportId.IsNull || viewportId.IsErased)
            return null;

        return viewportId;
    }

    /// <summary>
    /// 获取用户框选的视口 ObjectId 列表（支持窗口/ Crossing 框选）
    /// 如果用户未选择任何视口，返回 GetAllLayoutViewports()（当前布局所有视口窗口，不含 Number=1）
    /// </summary>
    /// <summary>
    /// 检查是否有预选的视口（命令前已框选的）
    /// </summary>
    public static List<ObjectId>? GetPreSelectedViewports()
    {
        var result = new List<ObjectId>();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return null;

        var ed = doc.Editor;
        var psr = ed.SelectImplied();
        if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            return null;

        foreach (SelectedObject so in psr.Value)
        {
            using var tr = doc.Database.TransactionManager.StartTransaction();
            if (so.ObjectId.ObjectClass.DxfName == "VIEWPORT")
            {
                var vp = (Viewport)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                if (vp.Number > 1)
                    result.Add(so.ObjectId);
            }
            tr.Commit();
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// 恢复指定视口为当前选择集
    /// </summary>
    public static void RestoreSelection(List<ObjectId> viewportIds)
    {
        if (viewportIds == null || viewportIds.Count == 0) return;
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        doc.Editor.SetImpliedSelection(viewportIds.ToArray());
    }

    /// <summary>
    /// 交互式选择视口（用户可框选，按 Esc 取消返回 null）
    /// </summary>
    public static List<ObjectId>? GetSelectedViewportsInteractive()
    {
        var result = new List<ObjectId>();

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return null;

        var ed = doc.Editor;

        var pso = new PromptSelectionOptions
        {
            MessageForAdding = "\n选择视口（支持框选，按 Enter 确认，Esc 取消）: ",
            AllowDuplicates = false
        };

        var filter = new SelectionFilter(new TypedValue[]
        {
            new TypedValue(0, "VIEWPORT")
        });

        var psr = ed.GetSelection(pso, filter);

        // 用户取消（Esc）或未选择任何视口
        if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            return null;

        foreach (SelectedObject so in psr.Value)
        {
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var vp = (Viewport)tr.GetObject(so.ObjectId, OpenMode.ForRead);
            if (vp.Number > 1)
            {
                result.Add(so.ObjectId);
            }
            tr.Commit();
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// 根据作用域字符串返回对应的视口列表
    /// </summary>
    public static List<ObjectId> GetViewportsByScope(string scope)
    {
        return scope switch
        {
            "AllLayoutViewports" => GetAllLayoutViewports(),
            "CurrentViewport" => GetCurrentViewport() is ObjectId id && !id.IsNull
                ? new List<ObjectId> { id }
                : new List<ObjectId>(),
            "SelectedViewports" => GetSelectedViewportsInteractive() ?? new List<ObjectId>(),
            _ => GetAllLayoutViewports()
        };
    }
}
