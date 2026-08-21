// filepath: SetVP/Commands/SetVPCommands.cs
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;

using SetVP.Services;
using SetVP.UI;

// 类型别名：彻底解决同名类型冲突
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsApplication = System.Windows.Forms.Application;

namespace SetVP.Commands;

/// <summary>
/// SetVP 插件命令入口类
/// [CommandMethod("SETVP")] 使其在 AutoCAD 命令行中可用
/// </summary>
public class SetVPCommands
{
    [CommandMethod("SETVP")]
    public void Run()
    {
        try
        {
            // 1. 加载已保存的设置（如果存在）
            var saved = SettingsManager.Load();
            var settings = saved ?? SelectionStateService.GetDefaultSettings();

            // 2. 解析当前视口作用域
            settings.ViewportScope =
                SelectionStateService.ResolveCurrentScope(settings);

            // 3. 如果没有历史记录，默认选中所有参照图层
            if (saved == null)
                settings.SelectAllReferenceLayers = true;

            // 4. 在显示窗体前捕获预选的视口（ShowModelessDialog 会清除 AutoCAD 选择集）
            var preSelectedViewportIds = ViewportSelectionService.GetPreSelectedViewports();

            // 5. 创建主窗体（modeless，不阻塞命令行）
            var form = new SetVpForm(settings, preSelectedViewportIds);

            // 关键修正（PLUGIN_DESIGN.md 11.1 节）：
            // Application.ShowModelessDialog 在 AutoCAD + WinForms 混合代码中存在类型歧义。
            // 使用显式别名 AcadApplication 明确调用 AutoCAD API 的方法，
            // 避免 WinForms Application 冲突。
            AcadApplication.ShowModelessDialog(form);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            var doc = AcadApplication.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage($"\nSetVP 加载失败: {ex.Message}\n");
        }
        catch (System.Exception ex)
        {
            var doc = AcadApplication.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage($"\nSetVP 错误: {ex.Message}\n");
        }
    }

    // 临时停用：诊断命令已迁移前先注释，避免影响主命令编译和运行
    // /// <summary>
    // /// 诊断命令：在 AutoCAD 命令行输出 Viewport 扩展字典中的图层替代信息
    // /// </summary>
    // [CommandMethod("SETVPCHECK")]
    // public void CheckViewportApi()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //
    //     var type = typeof(Viewport);
    //
    //     ed.WriteMessage("\n=== Viewport methods with 'Layer' ===");
    //     foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (m.Name.Contains("Layer", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //     }
    //
    //     ed.WriteMessage("\n\n=== Viewport methods with 'Override' ===");
    //     foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (m.Name.Contains("Override", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //     }
    //
    //     ed.WriteMessage("\n\n=== Viewport methods with 'Color' ===");
    //     foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (m.Name.Contains("Color", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //     }
    //
    //     ed.WriteMessage("\n\n=== Viewport properties with 'Layer' ===");
    //     foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (p.Name.Contains("Layer", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {p.Name} : {p.PropertyType.Name}");
    //     }
    //
    //     ed.WriteMessage("\n\n=== Viewport properties with 'Override' ===");
    //     foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (p.Name.Contains("Override", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {p.Name} : {p.PropertyType.Name}");
    //     }
    //
    //     ed.WriteMessage("\n\n=== Viewport properties with 'Color' ===");
    //     foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (p.Name.Contains("Color", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {p.Name} : {p.PropertyType.Name}");
    //     }
    //
    //     ed.WriteMessage("\n");
    // }

    // /// <summary>
    // /// 诊断命令：检查视口扩展字典中是否有图层替代数据
    // /// </summary>
    // [CommandMethod("SETVPVP")]
    // public void CheckViewportExtensionDict()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //
    //     var db = doc.Database;
    //     using var tr = db.TransactionManager.StartTransaction();
    //
    //     // 获取当前视口
    //     var vpId = doc.Editor.CurrentViewportObjectId;
    //     if (vpId.IsNull)
    //     {
    //         ed.WriteMessage("\n当前没有视口");
    //         return;
    //     }
    //
    //     var vp = (Viewport)tr.GetObject(vpId, OpenMode.ForRead);
    //     ed.WriteMessage($"\n视口 ObjectId: {vpId}");
    //     ed.WriteMessage($"\n是否有 ExtensionDictionary: {!vp.ExtensionDictionary.IsNull}");
    //
    //     if (!vp.ExtensionDictionary.IsNull)
    //     {
    //         var extDict = (DBDictionary)tr.GetObject(vp.ExtensionDictionary, OpenMode.ForRead);
    //         ed.WriteMessage($"\n扩展字典条目数: {extDict.Count}");
    //         foreach (var entry in extDict)
    //         {
    //             ed.WriteMessage($"\n  [{entry.Key}] -> {entry.Value.ObjectClass.Name}");
    //         }
    //     }
    //
    //     // 尝试获取 AcDbViewport 的原生接口
    //     try
    //     {
    //         var rxId = RXObject.GetClass(typeof(Viewport));
    //         ed.WriteMessage($"\nViewport RXClass name: {rxId.Name}");
    //     }
    //     catch (System.Exception ex)
    //     {
    //         ed.WriteMessage($"\nRXClass error: {ex.Message}");
    //     }
    //
    //     tr.Commit();
    // }

    // /// <summary>
    // /// 诊断命令：检查 LayerTableRecord 的所有属性/方法（查找 per-viewport 颜色相关）
    // /// </summary>
    // [CommandMethod("SETVPLYR")]
    // public void CheckLayerTableRecordApi()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //
    //     var type = typeof(LayerTableRecord);
    //
    //     ed.WriteMessage("\n=== LayerTableRecord methods with 'Viewport' or 'VP' or 'Override' ===");
    //     foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (m.Name.Contains("Viewport", StringComparison.OrdinalIgnoreCase) ||
    //             m.Name.Contains("VP", StringComparison.OrdinalIgnoreCase) ||
    //             m.Name.Contains("Override", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //     }
    //
    //     ed.WriteMessage("\n\n=== LayerTableRecord properties with 'Viewport' or 'VP' or 'Override' ===");
    //     foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (p.Name.Contains("Viewport", StringComparison.OrdinalIgnoreCase) ||
    //             p.Name.Contains("VP", StringComparison.OrdinalIgnoreCase) ||
    //             p.Name.Contains("Override", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"\n  {p.Name} : {p.PropertyType.Name}");
    //     }
    //
    //     ed.WriteMessage("\n");
    // }

    // /// <summary>
    // /// 诊断命令：获取 GetViewportOverrides 返回对象的类型和方法
    // /// </summary>
    // [CommandMethod("SETVPOVR")]
    // public void CheckViewportOverrides()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     // 获取当前视口
    //     var vpId = doc.Editor.CurrentViewportObjectId;
    //     if (vpId.IsNull)
    //     {
    //         ed.WriteMessage("\n当前没有视口");
    //         return;
    //     }
    //
    //     // 获取第一个包含 "|" 的图层（xref 图层）
    //     using var tr = db.TransactionManager.StartTransaction();
    //     var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    //     LayerTableRecord? foundLayer = null;
    //     foreach (ObjectId layerId in lt)
    //     {
    //         var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
    //         if (ltr.Name.Contains('|'))
    //         {
    //             foundLayer = ltr;
    //             break;
    //         }
    //     }
    //
    //     if (foundLayer == null)
    //     {
    //         ed.WriteMessage("\n未找到 xref 图层");
    //         return;
    //     }
    //
    //     ed.WriteMessage($"\n测试图层: {foundLayer.Name}");
    //     ed.WriteMessage($"\n视口 ObjectId: {vpId}");
    //     ed.WriteMessage($"\nHasOverrides: {foundLayer.HasViewportOverrides(vpId)}");
    //
    //     // 调用 GetViewportOverrides
    //     try
    //     {
    //         var overrides = foundLayer.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"\nGetViewportOverrides 返回类型: {overrides?.GetType().Name ?? "null"}");
    //
    //         if (overrides != null)
    //         {
    //             var overType = overrides.GetType();
    //             ed.WriteMessage($"\n=== 返回对象的方法 ===");
    //             foreach (var m in overType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //             {
    //                 ed.WriteMessage($"\n  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //             }
    //             ed.WriteMessage($"\n=== 返回对象的属性 ===");
    //             foreach (var p in overType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //             {
    //                 ed.WriteMessage($"\n  {p.Name} : {p.PropertyType.Name}");
    //             }
    //         }
    //     }
    //     catch (System.Exception ex)
    //     {
    //         ed.WriteMessage($"\nGetViewportOverrides 出错: {ex.Message}");
    //     }
    //
    //     tr.Commit();
    // }

    // /// <summary>
    // /// 诊断命令：测试视口图层颜色覆盖是否生效
    // /// 使用 ViewportSelectionService.GetAllLayoutViewports() 获取 Number>1 的视口窗口
    // /// </summary>
    // [CommandMethod("SETVPTEST")]
    // public void TestViewportColorOverride()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     // 获取当前布局的所有视口窗口（Number > 1）
    //     var viewportIds = ViewportSelectionService.GetAllLayoutViewports();
    //     if (viewportIds.Count == 0)
    //     {
    //         ed.WriteMessage("\n当前布局没有视口窗口（Number > 1）");
    //         return;
    //     }
    //
    //     // 使用第一个视口窗口进行测试
    //     var vpId = viewportIds.First();
    //
    //     // 获取第一个 xref 图层
    //     using var tr = db.TransactionManager.StartTransaction();
    //     var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    //     LayerTableRecord? foundLayer = null;
    //     foreach (ObjectId layerId in lt)
    //     {
    //         var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
    //         if (ltr.Name.Contains('|'))
    //         {
    //             foundLayer = ltr;
    //             break;
    //         }
    //     }
    //
    //     if (foundLayer == null)
    //     {
    //         ed.WriteMessage("\n未找到 xref 图层");
    //         return;
    //     }
    //
    //     ed.WriteMessage($"\n测试图层: {foundLayer.Name}");
    //     ed.WriteMessage($"\n视口 ObjectId: {vpId}");
    //     ed.WriteMessage($"\n图层 ObjectId: {foundLayer.Id}");
    //
    //     // 测试设置覆盖
    //     using (doc.LockDocument())
    //     {
    //         using var tr2 = db.TransactionManager.StartOpenCloseTransaction();
    //         var ltrWrite = (LayerTableRecord)tr2.GetObject(foundLayer.Id, OpenMode.ForWrite);
    //         var lvp = ltrWrite.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"\n设置前 IsColorOverridden: {lvp.IsColorOverridden}");
    //         ed.WriteMessage($"\n设置前 Color: {lvp.Color}");
    //
    //         // 方法1：通过 LayerViewportProperties 设置
    //         lvp.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)1);
    //         lvp.IsColorOverridden = true;
    //         ed.WriteMessage($"\n设置后 IsColorOverridden: {lvp.IsColorOverridden}");
    //
    //         // 验证设置是否生效（再次获取）
    //         var lvp2 = ltrWrite.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"\n重新获取 IsColorOverridden: {lvp2.IsColorOverridden}");
    //
    //         tr2.Commit();
    //     }
    //
    //     ed.WriteMessage($"\n已提交，请检查视口颜色是否变化");
    //     doc.Editor.Regen();
    // }

    // /// <summary>
    // /// 诊断命令：列出当前布局的所有视口 ObjectId
    // /// </summary>
    // [CommandMethod("SETVPALLVP")]
    // public void ListAllViewports()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     using var tr = db.TransactionManager.StartTransaction();
    //     var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    //
    //     ed.WriteMessage("\n=== 所有 Layout 的 Viewport ===");
    //     foreach (ObjectId btrId in bt)
    //     {
    //         if (btrId == bt[BlockTableRecord.ModelSpace]) continue;
    //
    //         var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
    //         ed.WriteMessage($"\nLayout: {btr.Name}");
    //
    //         foreach (ObjectId id in btr)
    //         {
    //             if (id.ObjectClass.Name == "AcDbViewport")
    //             {
    //                 ed.WriteMessage($"  Viewport Id: {id}");
    //
    //                 // 尝试读取视口的属性
    //                 var vp = (Viewport)tr.GetObject(id, OpenMode.ForRead);
    //                 ed.WriteMessage($"    Number: {vp.Number}");
    //                 // ViewportOn 和 Is_UCS_Associated 在 AutoCAD 2026 API 中不存在，跳过
    //             }
    //         }
    //     }
    //
    //     ed.WriteMessage($"\n当前 Editor.CurrentViewportObjectId: {doc.Editor.CurrentViewportObjectId}");
    //     ed.WriteMessage($"\n当前文档 MdiActiveDocument: {doc.Name}");
    //     tr.Commit();
    // }

    // /// <summary>
    // /// 诊断命令：全面探测视口图层替代 API
    // /// 测试 SetColorOverride / GetViewportOverrides 的各种用法
    // /// 使用 ViewportSelectionService.GetAllLayoutViewports() 获取 Number>1 的视口窗口
    // /// </summary>
    // [CommandMethod("SETVPPROBE")]
    // public void ProbeViewportOverrideApi()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     // 获取当前布局的所有视口窗口（Number > 1）
    //     var viewportIds = ViewportSelectionService.GetAllLayoutViewports();
    //     if (viewportIds.Count == 0)
    //     {
    //         ed.WriteMessage("\n当前布局没有视口窗口（Number > 1）");
    //         return;
    //     }
    //
    //     // 使用第一个视口窗口进行测试
    //     var vpId = viewportIds.First();
    //     ed.WriteMessage($"\n测试视口 ID: {vpId} (Number > 1 的视口窗口)");
    //
    //     // 获取第一个 xref 图层
    //     using var tr = db.TransactionManager.StartTransaction();
    //     var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    //     LayerTableRecord? targetLayer = null;
    //     foreach (ObjectId layerId in lt)
    //     {
    //         var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
    //         if (ltr.Name.Contains('|'))
    //         {
    //             targetLayer = ltr;
    //             break;
    //         }
    //     }
    //
    //     if (targetLayer == null)
    //     {
    //         ed.WriteMessage("\n未找到 xref 图层");
    //         tr.Commit();
    //         return;
    //     }
    //
    //     ed.WriteMessage($"\n目标图层: {targetLayer.Name} (Id={targetLayer.Id})");
    //     ed.WriteMessage($"\n视口 ID: {vpId}");
    //     ed.WriteMessage($"\nHasOverrides: {targetLayer.HasViewportOverrides(vpId)}");
    //
    //     // 获取当前覆盖状态
    //     var lvp = targetLayer.GetViewportOverrides(vpId);
    //     ed.WriteMessage($"\n当前 IsColorOverridden: {lvp.IsColorOverridden}");
    //     ed.WriteMessage($"当前 Color: {lvp.Color}");
    //     ed.WriteMessage($"当前 Color.Method: {lvp.Color.ColorMethod}");
    //
    //     // 遍历 LayerViewportProperties 的所有方法
    //     ed.WriteMessage($"\n=== LayerViewportProperties 所有方法 ===");
    //     var lvpType = typeof(LayerViewportProperties);
    //     foreach (var m in lvpType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (!m.IsSpecialName)
    //             ed.WriteMessage($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
    //     }
    //
    //     // 遍历 LayerViewportProperties 的所有属性
    //     ed.WriteMessage($"\n=== LayerViewportProperties 所有属性 ===");
    //     foreach (var p in lvpType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         ed.WriteMessage($"  {p.Name} : {p.PropertyType.Name}");
    //     }
    //
    //     // 遍历 LayerTableRecord 中所有包含 Viewport 的方法
    //     ed.WriteMessage($"\n=== LayerTableRecord 所有 Viewport 相关方法 ===");
    //     var ltrType = typeof(LayerTableRecord);
    //     foreach (var m in ltrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    //     {
    //         if (m.Name.Contains("Viewport", StringComparison.OrdinalIgnoreCase))
    //             ed.WriteMessage($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //     }
    //
    //     tr.Commit();
    //
    //     // 测试：尝试调用 SetColorOverride（底层方法）
    //     ed.WriteMessage($"\n\n=== 测试 SetColorOverride ===");
    //     try
    //     {
    //         using (doc.LockDocument())
    //         {
    //             using var tr2 = db.TransactionManager.StartOpenCloseTransaction();
    //             var ltrWrite = (LayerTableRecord)tr2.GetObject(targetLayer.Id, OpenMode.ForWrite);
    //
    //             // 检查是否有 SetColorOverride 方法
    //             var setColorMethod = ltrType.GetMethod("SetColorOverride", BindingFlags.Public | BindingFlags.Instance);
    //             ed.WriteMessage($"\nSetColorOverride 方法存在: {setColorMethod != null}");
    //             if (setColorMethod != null)
    //             {
    //                 ed.WriteMessage($"  参数: {string.Join(", ", setColorMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");
    //             }
    //
    //             // 尝试直接调用
    //             var testColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)10);
    //             try
    //             {
    //                 setColorMethod?.Invoke(ltrWrite, new object[] { testColor, vpId });
    //                 ed.WriteMessage("SetColorOverride 调用成功！");
    //             }
    //             catch (System.Exception ex)
    //             {
    //                 ed.WriteMessage($"SetColorOverride 调用失败: {ex.InnerException?.Message ?? ex.Message}");
    //             }
    //
    //             tr2.Commit();
    //         }
    //     }
    //     catch (System.Exception ex)
    //     {
    //         ed.WriteMessage($"\nSetColorOverride 测试出错: {ex.Message}");
    //     }
    //
    //     // 测试 RemoveAllOverrides
    //     ed.WriteMessage($"\n=== 测试 RemoveAllOverrides ===");
    //     try
    //     {
    //         using (doc.LockDocument())
    //         {
    //             using var tr2 = db.TransactionManager.StartOpenCloseTransaction();
    //             var ltrWrite = (LayerTableRecord)tr2.GetObject(targetLayer.Id, OpenMode.ForWrite);
    //             ltrWrite.RemoveAllOverrides();
    //             tr2.Commit();
    //             ed.WriteMessage("RemoveAllOverrides 调用成功");
    //         }
    //     }
    //     catch (System.Exception ex)
    //     {
    //         ed.WriteMessage($"RemoveAllOverrides 调用失败: {ex.Message}");
    //     }
    //
    //     ed.WriteMessage($"\n探测完成");
    // }

    // /// <summary>
    // /// 诊断命令：在同一事务内设置颜色覆盖并立即读回，验证设置是否生效
    // /// 使用 ViewportSelectionService.GetAllLayoutViewports() 获取 Number>1 的视口窗口
    // /// </summary>
    // [CommandMethod("SETVPSET")]
    // public void TestSetColorOverride()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     // 获取当前布局的所有视口窗口（Number > 1）
    //     var viewportIds = ViewportSelectionService.GetAllLayoutViewports();
    //     if (viewportIds.Count == 0)
    //     {
    //         ed.WriteMessage("\n当前布局没有视口窗口（Number > 1）");
    //         return;
    //     }
    //
    //     // 使用第一个视口窗口进行测试
    //     var vpId = viewportIds.First();
    //     ed.WriteMessage($"\n测试视口 ID: {vpId} (Number > 1 的视口窗口)");
    //
    //     // 找到第一个 xref 图层
    //     LayerTableRecord? targetLayer = null;
    //     ObjectId targetLayerId = ObjectId.Null;
    //     using (var tr = db.TransactionManager.StartTransaction())
    //     {
    //         var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    //         foreach (ObjectId layerId in lt)
    //         {
    //             var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
    //             if (ltr.Name.Contains('|'))
    //             {
    //                 targetLayer = ltr;
    //                 targetLayerId = layerId;
    //                 break;
    //             }
    //         }
    //         tr.Commit();
    //     }
    //
    //     if (targetLayer == null)
    //     {
    //         ed.WriteMessage("\n未找到 xref 图层");
    //         return;
    //     }
    //
    //     ed.WriteMessage($"\n目标图层: {targetLayer.Name} (Id={targetLayerId})");
    //
    //     // 测试1：在同一事务内设置并读回
    //     ed.WriteMessage($"\n=== 测试1：同一事务内设置颜色覆盖 ===");
    //     using (doc.LockDocument())
    //     {
    //         using var tr = db.TransactionManager.StartOpenCloseTransaction();
    //         var ltr = (LayerTableRecord)tr.GetObject(targetLayerId, OpenMode.ForWrite);
    //         var lvp = ltr.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"设置前 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         // 设置为红色 ACI 10
    //         var redColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)10);
    //         lvp.Color = redColor;
    //         lvp.IsColorOverridden = true;
    //         ed.WriteMessage($"设置后(未提交) - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         tr.Commit();
    //         ed.WriteMessage("事务已提交");
    //     }
    //
    //     // 测试2：重新开启事务，读回验证
    //     ed.WriteMessage($"\n=== 测试2：新事务内读回验证 ===");
    //     using (var tr2 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr2 = (LayerTableRecord)tr2.GetObject(targetLayerId, OpenMode.ForRead);
    //         var hasOvr = ltr2.HasViewportOverrides(vpId);
    //         ed.WriteMessage($"HasOverrides: {hasOvr}");
    //         var lvp2 = ltr2.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"读回 - IsColorOverridden: {lvp2.IsColorOverridden}, Color: {lvp2.Color}");
    //         tr2.Commit();
    //     }
    //
    //     // 强制 Regen
    //     ed.WriteMessage($"\n执行 Regen...");
    //     doc.Editor.Regen();
    //
    //     // 测试3：Regen 后再次读回
    //     ed.WriteMessage($"\n=== 测试3：Regen 后读回 ===");
    //     using (var tr3 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr3 = (LayerTableRecord)tr3.GetObject(targetLayerId, OpenMode.ForRead);
    //         var hasOvr3 = ltr3.HasViewportOverrides(vpId);
    //         ed.WriteMessage($"HasOverrides: {hasOvr3}");
    //         var lvp3 = ltr3.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"读回 - IsColorOverridden: {lvp3.IsColorOverridden}, Color: {lvp3.Color}");
    //         tr3.Commit();
    //     }
    //
    //     ed.WriteMessage($"\n诊断完成。请检查视口显示是否有变化。");
    // }

    // /// <summary>
    // /// 诊断命令：测试 LayerViewportProperties 是否为值类型（struct）
    // /// 使用 ViewportSelectionService.GetAllLayoutViewports() 获取 Number>1 的视口窗口
    // /// </summary>
    // [CommandMethod("SETVPSET2")]
    // public void TestSetColorOverride2()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     // 获取当前布局的所有视口窗口（Number > 1）
    //     var viewportIds = ViewportSelectionService.GetAllLayoutViewports();
    //     if (viewportIds.Count == 0)
    //     {
    //         ed.WriteMessage("\n当前布局没有视口窗口（Number > 1）");
    //         return;
    //     }
    //
    //     // 使用第一个视口窗口进行测试
    //     var vpId = viewportIds.First();
    //     ed.WriteMessage($"\n测试视口 ID: {vpId} (Number > 1 的视口窗口)");
    //
    //     // 找到第一个 xref 图层
    //     LayerTableRecord? targetLayer = null;
    //     ObjectId targetLayerId = ObjectId.Null;
    //     using (var tr = db.TransactionManager.StartTransaction())
    //     {
    //         var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    //         foreach (ObjectId layerId in lt)
    //         {
    //             var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
    //             if (ltr.Name.Contains('|'))
    //             { targetLayer = ltr; targetLayerId = layerId; break; }
    //         }
    //         tr.Commit();
    //     }
    //     if (targetLayer == null) { ed.WriteMessage("\n未找到 xref 图层"); return; }
    //     ed.WriteMessage($"\n目标图层: {targetLayer.Name} (Id={targetLayerId})");
    //
    //     // 测试：检查 LayerViewportProperties 的类型
    //     ed.WriteMessage($"\n=== LayerViewportProperties 类型检查 ===");
    //     var lvpType = typeof(LayerViewportProperties);
    //     ed.WriteMessage($"IsValueType: {lvpType.IsValueType}");
    //     ed.WriteMessage($"IsClass: {lvpType.IsClass}");
    //     ed.WriteMessage($"BaseType: {lvpType.BaseType}");
    //
    //     // 测试：检查 LayerViewportProperties.Color 属性的 setter
    //     ed.WriteMessage($"\n=== LayerViewportProperties.Color 属性检查 ===");
    //     var colorProp = lvpType.GetProperty("Color");
    //     if (colorProp != null)
    //     {
    //         ed.WriteMessage($"Color 属性 - CanWrite: {colorProp.CanWrite}, CanRead: {colorProp.CanRead}");
    //         var setMethod2 = colorProp.GetSetMethod();
    //         ed.WriteMessage($"Color setter 方法: {(setMethod2 != null ? setMethod2.Name : "null")}");
    //         ed.WriteMessage($"Color getter 方法: {colorProp.GetGetMethod()?.Name}");
    //     }
    //
    //     // 测试：检查 SetViewportOverrides 方法签名
    //     ed.WriteMessage($"\n=== SetViewportOverrides 方法检查 ===");
    //     var ltrType = typeof(LayerTableRecord);
    //     var setMethod = ltrType.GetMethod("SetViewportOverrides");
    //     if (setMethod != null)
    //     {
    //         ed.WriteMessage($"找到 SetViewportOverrides！参数: {string.Join(", ", setMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");
    //     }
    //     else
    //     {
    //         ed.WriteMessage("SetViewportOverrides 方法不存在！");
    //     }
    //
    //     // 测试：检查 LayerTableRecord 所有包含 "Override" 的方法
    //     ed.WriteMessage($"\n=== LayerTableRecord 所有 Override 相关方法 ===");
    //     foreach (var m in ltrType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
    //     {
    //         if (m.Name.Contains("Override", StringComparison.OrdinalIgnoreCase))
    //         {
    //             ed.WriteMessage($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    //         }
    //     }
    //
    //     // 测试：获取→修改→存回 模式
    //     ed.WriteMessage($"\n=== 测试获取-修改-存回模式 ===");
    //     using (doc.LockDocument())
    //     {
    //         using var tr = db.TransactionManager.StartOpenCloseTransaction();
    //         var ltr = (LayerTableRecord)tr.GetObject(targetLayerId, OpenMode.ForWrite);
    //
    //         // 获取当前覆盖属性
    //         var lvp = ltr.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"修改前 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         // 尝试直接修改 Color 属性
    //         var redColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)10);
    //         lvp.Color = redColor;
    //         lvp.IsColorOverridden = true;
    //         ed.WriteMessage($"修改后 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         // 尝试调用 SetViewportOverrides（如果存在）
    //         if (setMethod != null)
    //         {
    //             try {
    //                 setMethod.Invoke(ltr, new object[] { vpId, lvp });
    //                 ed.WriteMessage("SetViewportOverrides 调用成功！");
    //             } catch (System.Exception ex) {
    //                 ed.WriteMessage($"调用失败: {ex.InnerException?.Message ?? ex.Message}");
    //             }
    //         }
    //
    //         tr.Commit();
    //     }
    //
    //     // 读回验证
    //     ed.WriteMessage($"\n=== 读回验证 ===");
    //     using (var tr2 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr2 = (LayerTableRecord)tr2.GetObject(targetLayerId, OpenMode.ForRead);
    //         var lvp2 = ltr2.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"读回 - IsColorOverridden: {lvp2.IsColorOverridden}, Color: {lvp2.Color}");
    //         tr2.Commit();
    //     }
    //
    //     // 测试2：正确的顺序 - 先 IsColorOverridden=false，再 Color，最后 IsColorOverridden=true
    //     ed.WriteMessage($"\n=== 测试2：正确的设置顺序 ===");
    //     using (doc.LockDocument())
    //     {
    //         using var tr = db.TransactionManager.StartOpenCloseTransaction();
    //         var ltr = (LayerTableRecord)tr.GetObject(targetLayerId, OpenMode.ForWrite);
    //         var lvp = ltr.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"初始 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         var blueColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)5);
    //
    //         lvp.IsColorOverridden = false; // 第一步：先设置为 false
    //         ed.WriteMessage($"设置 IsColorOverridden=False后 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         lvp.Color = blueColor; // 第二步：再设置 Color
    //         ed.WriteMessage($"设置 Color=Blue(5)后 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         lvp.IsColorOverridden = true; // 第三步：最后设置为 true
    //         ed.WriteMessage($"设置 IsColorOverridden=True后 - IsColorOverridden: {lvp.IsColorOverridden}, Color: {lvp.Color}");
    //
    //         tr.Commit();
    //     }
    //     ed.WriteMessage($"\n=== 读回验证2 ===");
    //     using (var tr2 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr2 = (LayerTableRecord)tr2.GetObject(targetLayerId, OpenMode.ForRead);
    //         var lvp2 = ltr2.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"读回 - IsColorOverridden: {lvp2.IsColorOverridden}, Color: {lvp2.Color}");
    //         tr2.Commit();
    //     }
    //     ed.WriteMessage($"\n=== 读回验证2 ===");
    //     using (var tr2 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr2 = (LayerTableRecord)tr2.GetObject(targetLayerId, OpenMode.ForRead);
    //         var lvp2 = ltr2.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"读回 - IsColorOverridden: {lvp2.IsColorOverridden}, Color: {lvp2.Color}");
    //         tr2.Commit();
    //     }
    //
    //     doc.Editor.Regen();
    //
    //     // 测试3：调用底层 private SetColorOverride 方法（正确参数顺序）
    //     ed.WriteMessage($"\n=== 测试3：通过反射调用底层 SetColorOverride ===");
    //     try
    //     {
    //         using (doc.LockDocument())
    //         {
    //             using var tr = db.TransactionManager.StartOpenCloseTransaction();
    //             var ltr = (LayerTableRecord)tr.GetObject(targetLayerId, OpenMode.ForWrite);
    //
    //             // 通过反射查找 private SetColorOverride 方法
    //             // 参数顺序: (Color color, ObjectId viewportId)
    //             var setColorPrivMethod = ltrType.GetMethod("SetColorOverride",
    //                 BindingFlags.NonPublic | BindingFlags.Instance);
    //             if (setColorPrivMethod != null)
    //             {
    //                 ed.WriteMessage($"找到 private SetColorOverride！参数: {string.Join(", ", setColorPrivMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");
    //                 var testColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)30); // 黄色
    //                 try {
    //                     // 参数顺序: (Color, ObjectId)
    //                     setColorPrivMethod.Invoke(ltr, new object[] { testColor, vpId });
    //                     ed.WriteMessage("private SetColorOverride 调用成功！");
    //                 } catch (System.Exception ex) {
    //                     ed.WriteMessage($"调用失败: {ex.InnerException?.Message ?? ex.Message}");
    //                 }
    //             }
    //             else
    //             {
    //                 ed.WriteMessage("private SetColorOverride 方法也不存在");
    //             }
    //             tr.Commit();
    //         }
    //     }
    //     catch (System.Exception ex)
    //     {
    //         ed.WriteMessage($"反射调用出错: {ex.Message}");
    //     }
    //
    //     // 最终读回
    //     ed.WriteMessage($"\n=== 最终读回 ===");
    //     using (var tr3 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr3 = (LayerTableRecord)tr3.GetObject(targetLayerId, OpenMode.ForRead);
    //         var lvp3 = ltr3.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"读回 - IsColorOverridden: {lvp3.IsColorOverridden}, Color: {lvp3.Color}");
    //         tr3.Commit();
    //     }
    //
    //     doc.Editor.Regen();
    //     ed.WriteMessage($"\n诊断完成。请检查视口显示是否有黄色（ACI 30）变化。");
    // }

    // /// <summary>
    // /// 诊断命令：列出所有视口的详细信息，对比 CurrentViewportObjectId
    // /// 重要：Number=1 是布局本身的视口，Number>1 才是视口窗口
    // /// Editor.CurrentViewportObjectId 返回的是 Number=1 的布局主视口
    // /// </summary>
    // [CommandMethod("SETVPVPLIST")]
    // public void ListViewportDetails()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     var currentVpId = doc.Editor.CurrentViewportObjectId;
    //     ed.WriteMessage($"\n=== 视口诊断 ===");
    //     ed.WriteMessage($"\nEditor.CurrentViewportObjectId: {currentVpId} (这是布局主视口，Number=1)");
    //     ed.WriteMessage($"\n有效视口窗口: Number > 1");
    //
    //     using var tr = db.TransactionManager.StartTransaction();
    //     var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    //
    //     // 只找 PaperSpace 并列出 AcDbViewport 类型的对象
    //     ed.WriteMessage($"\n=== 查找 PaperSpace 中的视口对象 ===");
    //     var psId = bt[BlockTableRecord.PaperSpace];
    //     var ps = (BlockTableRecord)tr.GetObject(psId, OpenMode.ForRead);
    //     ed.WriteMessage($"\nPaperSpace: {ps.Name}, Id={psId}");
    //
    //     int viewportCount = 0;
    //     foreach (ObjectId id in ps)
    //     {
    //         if (id.ObjectClass.Name == "AcDbViewport")
    //         {
    //             viewportCount++;
    //             var vp = (Viewport)tr.GetObject(id, OpenMode.ForRead);
    //             var isWindow = vp.Number > 1 ? "视口窗口" : "布局主视口";
    //             ed.WriteMessage($"  [{viewportCount}] AcDbViewport Id={id}, Number={vp.Number} - {isWindow}");
    //             ed.WriteMessage($"      === 当前视口?: {id == currentVpId} ===");
    //         }
    //     }
    //
    //     if (viewportCount == 0)
    //         ed.WriteMessage($"  PaperSpace 中没有找到 AcDbViewport 对象！");
    //
    //     // 检查 CurrentViewportObjectId 是什么类型
    //     if (!currentVpId.IsNull)
    //     {
    //         ed.WriteMessage($"\n=== CurrentViewportObjectId 详细信息 ===");
    //         ed.WriteMessage($"ObjectClass: {currentVpId.ObjectClass.Name}");
    //         ed.WriteMessage($"IsNull: {currentVpId.IsNull}");
    //         ed.WriteMessage($"IsErased: {currentVpId.IsErased}");
    //     }
    //
    //     // 遍历 BlockTable 找出所有包含 AcDbViewport 的布局（跳过 xref 块定义）
    //     ed.WriteMessage($"\n=== 遍历所有布局，查找 AcDbViewport（跳过 xref）===");
    //     foreach (ObjectId btrId in bt)
    //     {
    //         var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
    //         bool isModel = (btrId == bt[BlockTableRecord.ModelSpace]);
    //         bool isPs = (btrId == bt[BlockTableRecord.PaperSpace]);
    //
    //         // 跳过 xref 块定义（名称包含 |）
    //         if (btr.Name.Contains('|'))
    //         {
    //             ed.WriteMessage($"\n布局: {btr.Name} (xref 块定义，已跳过)");
    //             continue;
    //         }
    //
    //         // 只显示包含 AcDbViewport 的布局
    //         bool hasViewport = false;
    //         foreach (ObjectId id in btr)
    //         {
    //             if (id.ObjectClass.Name == "AcDbViewport")
    //             {
    //                 if (!hasViewport)
    //                 {
    //                     ed.WriteMessage($"\n布局: {btr.Name} (IsModel={isModel}, IsPaperSpace={isPs}), Id={btrId}");
    //                     hasViewport = true;
    //                 }
    //                 var vp = (Viewport)tr.GetObject(id, OpenMode.ForRead);
    //                 var isWindowStr = vp.Number > 1 ? "视口窗口" : "布局主视口";
    //                 ed.WriteMessage($"  AcDbViewport Id={id}, Number={vp.Number}, {isWindowStr}, === 当前视口?: {id == currentVpId}");
    //             }
    //         }
    //     }
    //
    //     // 总结：有效视口窗口数量
    //     ed.WriteMessage($"\n=== 总结 ===");
    //     ed.WriteMessage($"Editor.CurrentViewportObjectId 返回的是布局主视口 (Number=1)");
    //     ed.WriteMessage($"实际视口窗口是 Number > 1 的视口");
    //     ed.WriteMessage($"使用 ViewportSelectionService.GetAllLayoutViewports() 可获取有效的视口窗口列表");
    //
    //     tr.Commit();
    // }

    // /// <summary>
    // /// 诊断命令：测试不同的视口刷新/重生成方式
    // /// 使用 ViewportSelectionService.GetAllLayoutViewports() 获取 Number>1 的视口窗口
    // /// </summary>
    // [CommandMethod("SETVPSET3")]
    // public void TestViewportRefresh()
    // {
    //     var doc = AcadApplication.DocumentManager.MdiActiveDocument;
    //     if (doc == null) return;
    //     var ed = doc.Editor;
    //     var db = doc.Database;
    //
    //     // 获取当前布局的所有视口窗口（Number > 1）
    //     var viewportIds = ViewportSelectionService.GetAllLayoutViewports();
    //     if (viewportIds.Count == 0)
    //     {
    //         ed.WriteMessage("\n当前布局没有视口窗口（Number > 1）");
    //         return;
    //     }
    //
    //     // 使用第一个视口窗口进行测试
    //     var vpId = viewportIds.First();
    //     ed.WriteMessage($"\n测试视口 ID: {vpId} (Number > 1 的视口窗口)");
    //
    //     // 找到第一个 xref 图层
    //     LayerTableRecord? targetLayer = null;
    //     ObjectId targetLayerId = ObjectId.Null;
    //     using (var tr = db.TransactionManager.StartTransaction())
    //     {
    //         var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    //         foreach (ObjectId layerId in lt)
    //         {
    //             var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
    //             if (ltr.Name.Contains('|'))
    //             { targetLayer = ltr; targetLayerId = layerId; break; }
    //         }
    //         tr.Commit();
    //     }
    //     if (targetLayer == null) { ed.WriteMessage("\n未找到 xref 图层"); return; }
    //     ed.WriteMessage($"\n目标图层: {targetLayer.Name} (Id={targetLayerId})");
    //
    //     // 用 private SetColorOverride 设置为黄色（ACI 30）
    //     ed.WriteMessage($"\n=== 设置颜色为黄色(ACI 30) ===");
    //     using (doc.LockDocument())
    //     {
    //         using var tr = db.TransactionManager.StartOpenCloseTransaction();
    //         var ltr = (LayerTableRecord)tr.GetObject(targetLayerId, OpenMode.ForWrite);
    //         var setColorPrivMethod = typeof(LayerTableRecord).GetMethod("SetColorOverride",
    //             BindingFlags.NonPublic | BindingFlags.Instance);
    //         var yellowColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)30);
    //         setColorPrivMethod?.Invoke(ltr, new object[] { yellowColor, vpId });
    //         tr.Commit();
    //         ed.WriteMessage("设置完成");
    //     }
    //
    //     // 读回验证
    //     using (var tr2 = db.TransactionManager.StartTransaction())
    //     {
    //         var ltr2 = (LayerTableRecord)tr2.GetObject(targetLayerId, OpenMode.ForRead);
    //         var lvp2 = ltr2.GetViewportOverrides(vpId);
    //         ed.WriteMessage($"设置后 - IsColorOverridden: {lvp2.IsColorOverridden}, Color: {lvp2.Color}");
    //         tr2.Commit();
    //     }
    //
    //     // 测试各种刷新方式
    //     ed.WriteMessage($"\n=== 测试刷新方式 ===");
    //
    //     ed.WriteMessage($"\n1. ed.Regen()...");
    //     doc.Editor.Regen();
    //
    //     ed.WriteMessage($"\n请手动在命令行输入 REGEN 查看是否有变化。");
    //
    //     ed.WriteMessage($"\n请检查视口显示是否有黄色（ACI 30）变化。");
    // }
}
