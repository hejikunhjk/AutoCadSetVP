// filepath: SetVP/UI/SetVpForm.cs
using System.Drawing;
using System.Windows.Forms;

using SetVP.Models;
using SetVP.Services;
using SetVP.Commands;

// 类型别名：避免与 AutoCAD API 同名冲突
using GdiFont = System.Drawing.Font;
using GdiColor = System.Drawing.Color;
using AcadColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace SetVP.UI;

/// <summary>
/// 主窗体 — modeless WinForms 窗口
/// </summary>
public class SetVpForm : Form
{
    /// <summary>
    /// 静态实例引用（用于加载/卸载时关闭）
    /// </summary>
    private static SetVpForm? s_instance;

    private AppSettings _settings;

    /// <summary>
    /// DPI 缩放比例（1.0 = 100%, 1.5 = 150%）
    /// </summary>
    private readonly float _dpiScale = 1.0f;

    /// <summary>
    /// DPI 缩放辅助方法：将设计时像素值转换为当前 DPI 下的实际像素值
    /// </summary>
    private int S(int designPx) => (int)(designPx * _dpiScale);

    /// <summary>
    /// DPI 缩放辅助方法：将设计时像素值转换为当前 DPI 下的实际像素值（float）
    /// </summary>
    private float SF(float designPx) => designPx * _dpiScale;

    // UI 控件
    private readonly GroupBox _viewportScopeGroup;
    private readonly RadioButton _rbAllViewports;
    private readonly RadioButton _rbCurrentViewport;
    private readonly RadioButton _rbSelectedViewports;

    private readonly GroupBox _layersGroup;
    private readonly Button _btnSelectRefOnly;
    private readonly TextBox _txtLayerFilter;
    private readonly Button _btnSelectAllFiltered;
    private readonly Button _btnInvertFiltered;
    private readonly CheckedListBox _lbLayers;
    private List<ReferenceLayerInfo> _allReferenceLayers = new();

    private readonly GroupBox _colorGroup;

    private readonly Button _rbByLayer;
    private readonly Button _btnRgbColor;
    private readonly ColorDialog _colorDialog = new();

    private readonly Button _btnApply;
    private readonly Button _btnRestore;
    private bool _viewportSelectionDone;  // 是否已在本轮完成过视口框选
    private Size _sizeBeforeHide;         // 隐藏前的窗口尺寸（用于恢复）
    private readonly List<ObjectId>? _preSelectedViewportIds;  // 命令前已框选的视口（在命令入口处提前捕获）

    public SetVpForm(AppSettings settings, List<ObjectId>? preSelectedViewportIds)
    {
        _settings = settings;
        _preSelectedViewportIds = preSelectedViewportIds;

        // ========== 检测 DPI 缩放比例 ==========
        // 获取系统 DPI（默认 96，150% 缩放时为 144）
        using (var g = this.CreateGraphics())
        {
            float systemDpi = g.DpiX;
            _dpiScale = systemDpi / 96f;
        }

        // 窗体基本属性
        Text = "SetVP — 参照图层颜色快速替换";
        Size = new Size(S(452), S(400));  // 始终以最小尺寸打开
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(S(452), S(400));
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = GdiColor.FromArgb(240, 240, 240);
        KeyPreview = true;
        // 禁用 WinForms 内置缩放，改用手动 DPI 缩放
        AutoScaleMode = AutoScaleMode.None;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        // 字体声明（避免 AutoCAD Font 类冲突）
        // 字体大小乘以 DPI 缩放比例，确保在高 DPI 下字体足够大
        var fontLabel = new GdiFont("Segoe UI", 9F * _dpiScale, FontStyle.Regular);
        var fontButton = new GdiFont("Segoe UI", 9F * _dpiScale, FontStyle.Bold);

        // ========== 操作按钮（固定在底部）==========
        _btnApply = new Button
        {
            Text = "应用",
            Location = new Point(S(12), 0),
            Size = new Size(S(185), S(38)),
            Font = fontButton,
            BackColor = GdiColor.FromArgb(0, 120, 215),
            ForeColor = GdiColor.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnApply.Click += BtnApply_Click;

        _btnRestore = new Button
        {
            Text = "还原",
            Location = new Point(S(207), 0),
            Size = new Size(S(185), S(38)),
            Font = fontButton,
            BackColor = GdiColor.FromArgb(200, 200, 200),
            ForeColor = GdiColor.Black,
            FlatStyle = FlatStyle.Flat
        };
        _btnRestore.Click += BtnRestore_Click;

        // ========== 颜色选择（固定在按钮上方）==========
        // 布局：10列，列宽 38px，行高 30px
        // Row 0: ByLayer(0-4), RGB(5-9)
        // Row 1: ACI 1-5
        // Row 2: ACI 6-9
        // Row 3: Gray 250-254
        // Row 4: Gray 255-259
        // 总高：5 * 30 = 150px
        // ======================================

        var colorGrid = new TableLayoutPanel
        {
            Location = new Point(S(12), S(12)),
            Size = new Size(S(380), S(90)),
            ColumnCount = 10,
            RowCount = 3,
            BackColor = GdiColor.Transparent
        };

        // 10列×38px=380px
        for (int i = 0; i < 10; i++)
            colorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SF(38F)));
        colorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, SF(30F)));  // Row 0: ByLayer+RGB
        colorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, SF(30F)));  // Row 1: ACI 1-9
        colorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, SF(30F)));  // Row 2: ACI 250-259

        // ===== Row 0: ByLayer (0-4), RGB (5-9) =====
        _rbByLayer = new Button
        {
            Text = "ByLayer",
            FlatStyle = FlatStyle.Flat,
            Font = new GdiFont("Segoe UI", 8F * _dpiScale),
            BackColor = GdiColor.FromArgb(230, 230, 230),
            ForeColor = GdiColor.Black,
            Tag = "ByLayer",
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _rbByLayer.FlatAppearance.BorderColor = _settings.ColorMode == "ByLayer"
            ? GdiColor.FromArgb(0, 120, 215) : GdiColor.Gray;
        _rbByLayer.FlatAppearance.BorderSize = _settings.ColorMode == "ByLayer" ? 2 : 1;
        _rbByLayer.Click += AciColorButton_Click;
        colorGrid.Controls.Add(_rbByLayer, 0, 0);
        colorGrid.SetColumnSpan(_rbByLayer, 5);

        // RGB 按钮文字初始化
        var initialRgb = _settings.CustomColorRgb ?? "128,128,128";
        var rgbText = "RGB #" + RgbToHex(initialRgb);

        _btnRgbColor = new Button
        {
            Text = rgbText,
            FlatStyle = FlatStyle.Flat,
            Font = new GdiFont("Segoe UI", 8F * _dpiScale),
            BackColor = ParseRgbToGdiColor(initialRgb),
            ForeColor = GdiColor.Black,
            Tag = "CustomRgb",
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _btnRgbColor.FlatAppearance.BorderColor = _settings.ColorMode == "CustomRgb"
            ? GdiColor.FromArgb(0, 120, 215) : GdiColor.Gray;
        _btnRgbColor.FlatAppearance.BorderSize = _settings.ColorMode == "CustomRgb" ? 2 : 1;
        _btnRgbColor.Click += (s, e) =>
        {
            var customRgb = _settings.CustomColorRgb ?? "128,128,128";
            var parts = customRgb.Split(',');
            if (parts.Length == 3
                && byte.TryParse(parts[0], out var r)
                && byte.TryParse(parts[1], out var g)
                && byte.TryParse(parts[2], out var b))
                _colorDialog.Color = GdiColor.FromArgb(r, g, b);

            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                var c = _colorDialog.Color;
                _settings.CustomColorRgb = $"{c.R},{c.G},{c.B}";
                _btnRgbColor.BackColor = c;
                _btnRgbColor.Text = "RGB #" + RgbToHex(_settings.CustomColorRgb);
                SelectColorMode(colorGrid, "CustomRgb");
                _settings.ColorMode = "CustomRgb";
                UpdateColorGroupTitle();
            }
        };
        colorGrid.Controls.Add(_btnRgbColor, 5, 0);
        colorGrid.SetColumnSpan(_btnRgbColor, 5);

        // ===== Row 1: ACI 1-9 =====
        var aciColors1to9 = new (int index, GdiColor color)[]
        {
            (1, GdiColor.FromArgb(255, 0, 0)),
            (2, GdiColor.FromArgb(255, 255, 0)),
            (3, GdiColor.FromArgb(0, 255, 0)),
            (4, GdiColor.FromArgb(0, 255, 255)),
            (5, GdiColor.FromArgb(0, 0, 255)),
            (6, GdiColor.FromArgb(255, 0, 255)),
            (7, GdiColor.FromArgb(255, 255, 255)),
            (8, GdiColor.FromArgb(128, 128, 128)),
            (9, GdiColor.FromArgb(64, 64, 64)),
        };
        for (int i = 0; i < 9; i++)
        {
            var (idx, clr) = aciColors1to9[i];
            var btn = CreateAciColorButton(idx, clr);
            colorGrid.Controls.Add(btn, i, 1);
        }
        // col 9 空cell (row 1)
        colorGrid.Controls.Add(new Panel { BackColor = GdiColor.Transparent, Dock = DockStyle.Fill }, 9, 1);

        // ===== Row 2: ACI 250-259 =====
        var gray250to259 = new (int index, GdiColor color)[]
        {
            (250, GdiColor.FromArgb(147, 147, 147)),
            (251, GdiColor.FromArgb(193, 193, 193)),
            (252, GdiColor.FromArgb(214, 214, 214)),
            (253, GdiColor.FromArgb(105, 105, 105)),
            (254, GdiColor.FromArgb(137, 137, 137)),
            (255, GdiColor.FromArgb(170, 170, 170)),
            (256, GdiColor.FromArgb(118, 118, 118)),
            (257, GdiColor.FromArgb(146, 146, 146)),
            (258, GdiColor.FromArgb(170, 170, 170)),
            (259, GdiColor.FromArgb(185, 185, 185)),
        };
        for (int i = 0; i < 10; i++)
        {
            var (idx, clr) = gray250to259[i];
            var btn = CreateAciColorButton(idx, clr);
            colorGrid.Controls.Add(btn, i, 2);
        }

        // 颜色区 GroupBox（标题显示当前颜色）
        _colorGroup = new GroupBox
        {
            Location = new Point(S(12), 0),
            Size = new Size(S(404), S(102)),
            Font = fontLabel,
            Text = GetColorModeText()
        };
        _colorGroup.Controls.Add(colorGrid);

        // 初始化
        UpdateColorGroupTitle();
        SelectColorMode(colorGrid, _settings.ColorMode);

        // ========== 参考图层（中间可拉伸区，从下往上堆叠）==========
        // 视口组：固定 56px
        // 图层组：随窗口拉伸
        // 颜色组：固定 154px
        // 按钮组：固定 50px
        // 总固定高度：56 + 154 + 50 = 260px（不含边距）

        _viewportScopeGroup = new GroupBox
        {
            Text = "视口作用域",
            Location = new Point(S(12), 0),
            Size = new Size(S(380), S(56)),
            Font = fontLabel
        };

        var vpFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,  // 禁止换行，确保3个单选框在一行
            BackColor = GdiColor.Transparent
        };

        _rbAllViewports = new RadioButton
        {
            Text = "当前布局视口",
            AutoSize = true,
            Checked = _settings.ViewportScope == "AllLayoutViewports",
            Font = fontLabel
        };

        _rbCurrentViewport = new RadioButton
        {
            Text = "当前视口",
            AutoSize = true,
            Checked = _settings.ViewportScope == "CurrentViewport",
            Font = fontLabel
        };

        _rbSelectedViewports = new RadioButton
        {
            Text = "选择视口",
            AutoSize = true,
            Checked = _settings.ViewportScope == "SelectedViewports",
            Font = fontLabel
        };

        _rbSelectedViewports.CheckedChanged += (s, ev) => { _viewportSelectionDone = false; };
        vpFlow.Controls.AddRange(new Control[] { _rbAllViewports, _rbCurrentViewport, _rbSelectedViewports });
        _viewportScopeGroup.Controls.Add(vpFlow);

        // 控制行：只选参照 + 过滤文本框 + 全选 + 反选（FlowLayoutPanel，自动处理换行）
        var layerControlFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,  // 不换行，确保所有控件在一行内
            BackColor = GdiColor.Transparent
        };

        _btnSelectRefOnly = new Button
        {
            Text = "只选参照",
            Size = new Size(S(62), S(27)),
            Font = new GdiFont("Segoe UI", 8F * _dpiScale),
            FlatStyle = FlatStyle.Flat
        };
        _btnSelectRefOnly.Click += (s, e) =>
        {
            for (int i = 0; i < _lbLayers!.Items.Count; i++)
                _lbLayers.SetItemChecked(i, false);
            _settings.SelectAllReferenceLayers = true;
            LoadLayerList();
        };

        _txtLayerFilter = new TextBox
        {
            Text = "*",
            Size = new Size(S(90), S(27)),
            Font = fontLabel
        };
        _txtLayerFilter.TextChanged += (s, e) => ApplyLayerFilter();

        _btnSelectAllFiltered = new Button
        {
            Text = "全选",
            Size = new Size(S(50), S(27)),
            Font = new GdiFont("Segoe UI", 8F * _dpiScale),
            FlatStyle = FlatStyle.Flat
        };
        _btnSelectAllFiltered.Click += (s, e) =>
        {
            for (int i = 0; i < _lbLayers!.Items.Count; i++)
                _lbLayers.SetItemChecked(i, true);
        };

        _btnInvertFiltered = new Button
        {
            Text = "反选",
            Size = new Size(S(50), S(27)),
            Font = new GdiFont("Segoe UI", 8F * _dpiScale),
            FlatStyle = FlatStyle.Flat
        };
        _btnInvertFiltered.Click += (s, e) =>
        {
            for (int i = 0; i < _lbLayers!.Items.Count; i++)
                _lbLayers.SetItemChecked(i, !_lbLayers.GetItemChecked(i));
        };

        layerControlFlow.Controls.AddRange(new Control[] { _btnSelectRefOnly, _txtLayerFilter, _btnSelectAllFiltered, _btnInvertFiltered });

        _lbLayers = new CheckedListBox
        {
            Location = new Point(S(16), S(50)),
            Size = new Size(S(348), S(200)),
            Font = fontLabel,
            CheckOnClick = true
        };

        // 加载图层列表
        LoadLayerList();

        _layersGroup = new GroupBox
        {
            Text = "参照图层",
            Location = new Point(S(12), 0),
            Size = new Size(S(380), S(260)),
            Font = fontLabel
        };
        _layersGroup.Controls.Add(layerControlFlow);
        _layersGroup.Controls.Add(_lbLayers);

        // ========== 布局排列（从下往上）==========
        // 视口组在最上，图层组在中间（可拉伸），颜色组和按钮组固定在底部
        // 窗口 Resize 时只调整图层组的高度
        Controls.Add(_viewportScopeGroup);
        Controls.Add(_layersGroup);
        Controls.Add(_colorGroup);
        Controls.Add(_btnApply);
        Controls.Add(_btnRestore);

        // 统一 Resize 处理
        Resize += (s, e) => LayoutControls();

        // 初始布局
        LayoutControls();

        // 还原按钮：无修改记录时灰掉
        _btnRestore.Enabled = ColorOverrideService.HasOverrides();

        // 窗口尺寸重置（每次打开均为最小尺寸，不记忆）
        Shown += (s, ev) =>
        {
            ClientSize = MinimumSize;
            LayoutControls();

            // 延迟恢复预选视口（ShowModelessDialog 会清除选择集）
            // BeginInvoke(new Action(() =>
            // {
            //     if (_preSelectedViewportIds != null && _preSelectedViewportIds.Count > 0)
            //         ViewportSelectionService.RestoreSelection(_preSelectedViewportIds);
            // }));
            _ = _preSelectedViewportIds; // 未使用，暂时保留
        };
    }

    /// <summary>
    /// 从下往上布局：按钮→颜色组→图层组（中间拉伸）→视口组
    /// 所有尺寸乘以 DPI 缩放比例，确保高 DPI 下控件不溢出
    /// </summary>
    private void LayoutControls()
    {
        int w = ClientSize.Width - 24;   // 减去左右各 12px 边距
        int h = ClientSize.Height;
        int margin = (int)(12 * _dpiScale);
        int gap = (int)(4 * _dpiScale);

        // GroupBox 标题区域高度（随字体缩放）
        int titleH = TextRenderer.MeasureText("测量", _viewportScopeGroup.Font).Height + 6;

        // 按钮高度
        int btnH = (int)(38 * _dpiScale);

        // 颜色组高度：colorGrid 的 3 行 × 30px + 标题区域 + 上下边距
        int colorGridH = (int)(30 * _dpiScale) * 3;  // 3行颜色按钮
        int colorH = titleH + colorGridH + (int)(14 * _dpiScale);

        // 视口组高度：足够容纳一行 RadioButton（FlowLayoutPanel 自动布局）
        int vpH = titleH + (int)(30 * _dpiScale);

        // 固定高度
        int btnRowH = btnH + gap;

        // 可用高度给图层组（中间拉伸）
        int availH = h - btnRowH - colorH - vpH - gap * 3 - margin * 2;
        int layerH = Math.Max((int)(60 * _dpiScale), availH);

        // 视口组：最上
        _viewportScopeGroup.Location = new Point(margin, margin);
        _viewportScopeGroup.Size = new Size(w, vpH);

        // 视口组内的 FlowLayoutPanel 位置（标题下方，左对齐）
        var vpFlow = _viewportScopeGroup.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (vpFlow != null)
        {
            vpFlow.Location = new Point((int)(12 * _dpiScale), titleH - (int)(4 * _dpiScale));
            vpFlow.Size = new Size(w - (int)(24 * _dpiScale), (int)(30 * _dpiScale));
        }

        // 图层组：中间（从视口组下方延伸）
        _layersGroup.Location = new Point(margin, margin + vpH + gap);
        _layersGroup.Size = new Size(w, layerH);

        // 更新图层列表大小（填满 GroupBox 剩余空间）
        int layerControlRowH = (int)(30 * _dpiScale);  // 控制行高度
        int listTop = titleH + (int)(8 * _dpiScale) + layerControlRowH + (int)(5 * _dpiScale);
        int listH = Math.Max((int)(60 * _dpiScale), _layersGroup.Height - listTop - margin);
        _lbLayers.Location = new Point((int)(16 * _dpiScale), listTop);
        _lbLayers.Size = new Size(w - (int)(32 * _dpiScale), listH);

        // 图层组内的控制按钮行（FlowLayoutPanel）
        int layerControlY = titleH + (int)(6 * _dpiScale);
        var layerControlPanel = _layersGroup.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (layerControlPanel != null)
        {
            layerControlPanel.Location = new Point((int)(12 * _dpiScale), layerControlY - (int)(6 * _dpiScale));
            layerControlPanel.Size = new Size(w - (int)(24 * _dpiScale), layerControlRowH);
            layerControlPanel.BringToFront();  // 将按钮行前置，避免被列表框遮挡
        }

        // 颜色组：在图层组下方
        _colorGroup.Location = new Point(margin, margin + vpH + gap + layerH + gap);
        _colorGroup.Size = new Size(w, colorH);

        // 颜色组内的 grid 位置
        var colorGrid = _colorGroup.Controls[0] as TableLayoutPanel;
        if (colorGrid != null)
        {
            colorGrid.Location = new Point((int)(12 * _dpiScale), titleH + (int)(4 * _dpiScale));
        }

        // 按钮：在最底部
        int btnRowY = margin + vpH + gap + layerH + gap + colorH + gap;
        _btnApply.Location = new Point(margin, btnRowY);
        _btnApply.Size = new Size((w - gap) / 2, btnH);
        _btnRestore.Location = new Point(margin + (w - gap) / 2 + gap, btnRowY);
        _btnRestore.Size = new Size((w - gap) / 2, btnH);
    }

    /// <summary>
    /// 更新颜色组标题（显示当前选中颜色）
    /// </summary>
    private void UpdateColorGroupTitle()
    {
        _colorGroup!.Text = GetColorModeText();
    }

    /// <summary>
    /// 创建 ACI 颜色按钮
    /// </summary>
    private Button CreateAciColorButton(int aciIndex, GdiColor acadColor)
    {
        var btn = new Button
        {
            Text = $"{aciIndex}",
            Tag = aciIndex,
            FlatStyle = FlatStyle.Flat,
            Font = new GdiFont("Segoe UI", 7F * _dpiScale),
            BackColor = acadColor,
            ForeColor = (acadColor.GetBrightness() < 0.5f) ? GdiColor.White : GdiColor.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        btn.FlatAppearance.BorderSize = _settings.ColorMode == "ACI" && _settings.QuickColorIndex == aciIndex ? 2 : 1;
        btn.FlatAppearance.BorderColor = _settings.ColorMode == "ACI" && _settings.QuickColorIndex == aciIndex
            ? GdiColor.FromArgb(0, 120, 215) : GdiColor.Gray;

        btn.Click += AciColorButton_Click;
        return btn;
    }

    /// <summary>
    /// 颜色按钮点击事件
    /// </summary>
    private void AciColorButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var grid = btn.Parent as TableLayoutPanel;
        if (grid == null) return;

        var tag = btn.Tag;
        if (tag is int aciIndex)
        {
            // ACI 颜色
            _settings.ColorMode = "ACI";
            _settings.QuickColorIndex = aciIndex;
        }
        else if (tag is string modeStr)
        {
            _settings.ColorMode = modeStr;
        }

        SelectColorMode(grid, _settings.ColorMode);
        UpdateColorGroupTitle();
    }

    /// <summary>
    /// 获取当前颜色模式的文字描述
    /// </summary>
    private string GetColorModeText()
    {
        return _settings.ColorMode switch
        {
            "ByLayer" => "颜色：ByLayer（图层原色）",
            "ByBlock" => "颜色：ByBlock（图块原色）",
            "ACI" => $"颜色：ACI #{_settings.QuickColorIndex}",
            "CustomRgb" => $"颜色：RGB({_settings.CustomColorRgb})",
            _ => "颜色：请选择颜色模式"
        };
    }

    /// <summary>
    /// 更新颜色网格的选中边框样式
    /// </summary>
    private void SelectColorMode(TableLayoutPanel grid, string mode)
    {
        foreach (Control ctrl in grid.Controls)
        {
            if (ctrl is Button btn && btn != _rbByLayer)
            {
                var tag = btn.Tag;
                bool selected = false;
                if (tag is int aci && mode == "ACI")
                    selected = _settings.QuickColorIndex == aci;
                else if (tag is string m && m == mode)
                    selected = true;

                btn.FlatAppearance.BorderSize = selected ? 2 : 1;
                btn.FlatAppearance.BorderColor = selected ? GdiColor.FromArgb(0, 120, 215) : GdiColor.Gray;
            }
        }
    }

    private static GdiColor ParseRgbToGdiColor(string rgb)
    {
        var parts = rgb.Split(',');
        if (parts.Length == 3 &&
            byte.TryParse(parts[0], out var r) &&
            byte.TryParse(parts[1], out var g) &&
            byte.TryParse(parts[2], out var b))
            return GdiColor.FromArgb(r, g, b);
        return GdiColor.FromArgb(128, 128, 128);
    }

    /// <summary>
    /// 将 RGB 字符串转换为十六进制颜色代码
    /// </summary>
    private static string RgbToHex(string rgb)
    {
        var parts = rgb.Split(',');
        if (parts.Length == 3 &&
            byte.TryParse(parts[0], out var r) &&
            byte.TryParse(parts[1], out var g) &&
            byte.TryParse(parts[2], out var b))
            return $"{r:X2}{g:X2}{b:X2}";
        return "808080";
    }

    private void LoadLayerList()
    {
        _allReferenceLayers = LayerSelectionService.GetReferenceLayers(_settings);
        ApplyLayerFilter();
    }

    private void ApplyLayerFilter()
    {
        var filter = _txtLayerFilter?.Text ?? "*";
        if (string.IsNullOrWhiteSpace(filter) || filter == "*")
            filter = "";

        var savedChecked = new Dictionary<string, bool>();
        for (int i = 0; i < _lbLayers.Items.Count; i++)
        {
            var item = _lbLayers.Items[i]?.ToString() ?? "";
            savedChecked[item] = _lbLayers.GetItemChecked(i);
        }

        _lbLayers.Items.Clear();
        foreach (var layer in _allReferenceLayers)
        {
            var displayName = $"{layer.XrefName}|{layer.LayerName}";
            if (filter == "" || layer.LayerName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                var wasChecked = savedChecked.TryGetValue(displayName, out var v) && v;
                _lbLayers.Items.Add(displayName, layer.IsSelected || wasChecked);
            }
        }
    }

    private void BtnApply_Click(object? sender, EventArgs e)
    {
        // 更新设置
        _settings.ViewportScope = _rbAllViewports.Checked ? "AllLayoutViewports"
            : _rbCurrentViewport.Checked ? "CurrentViewport"
            : "SelectedViewports";

        _settings.SelectAllReferenceLayers = true;

        // 收集选中的图层
        _settings.SelectedLayerNames.Clear();
        foreach (var item in _lbLayers.CheckedItems)
        {
            _settings.SelectedLayerNames.Add(item.ToString()!);
        }

        // 解析视口
        List<ObjectId> viewportIds;
        if (_settings.ViewportScope == "SelectedViewports")
        {
            // A: 如果已经有预选的视口（命令前框选的），直接使用
            var preSelected = _preSelectedViewportIds;
            if (preSelected != null)
            {
                viewportIds = preSelected;
            }
            // B: 如果本轮已框选过，直接使用
            else if (_viewportSelectionDone)
            {
                viewportIds = ViewportSelectionService.GetViewportsByScope(_settings.ViewportScope);
            }
            // C: 没有预选且本轮未框选，隐藏窗体等用户框选
            else
            {
                _sizeBeforeHide = ClientSize;  // 保存隐藏前的尺寸
                Hide();
                var selected = ViewportSelectionService.GetSelectedViewportsInteractive();
                if (selected == null)
                {
                    // 用户取消（Esc），重新显示窗体（恢复隐藏前的尺寸）
                    ClientSize = _sizeBeforeHide;
                    LayoutControls();
                    Show();
                    return;
                }
                viewportIds = selected;
                _viewportSelectionDone = true;
                // 重新显示窗体（恢复隐藏前的尺寸）
                ClientSize = _sizeBeforeHide;
                LayoutControls();
                Show();
            }
        }
        else
        {
            viewportIds = ViewportSelectionService.GetViewportsByScope(_settings.ViewportScope);
        }

        if (viewportIds.Count == 0)
        {
            MessageBox.Show("未找到任何视口。", "SetVP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var targetLayers = LayerSelectionService.ResolveTargetLayers(_settings);
        if (targetLayers.Count == 0)
        {
            MessageBox.Show("没有要处理的图层。", "SetVP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var colorMode = _settings.ColorMode;

        if (colorMode == "ByLayer")
        {
            // ByLayer：获取每个图层各自的实际颜色作为 override 值
            ColorOverrideService.ApplyColorOverrideByLayerColor(viewportIds, targetLayers);
        }
        else if (colorMode == "ACI")
        {
            var color = ColorOverrideService.IndexToAcadColor(_settings.QuickColorIndex);
            ColorOverrideService.ApplyColorOverride(viewportIds, targetLayers, color);
        }
        else // CustomRgb
        {
            var color = ColorOverrideService.RgbToAcadColor(_settings.CustomColorRgb);
            ColorOverrideService.ApplyColorOverride(viewportIds, targetLayers, color);
        }

        SettingsManager.Save(_settings);

        // 强制完全刷新显示 (REGENALL)
        var acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        if (acDoc != null)
        {
            acDoc.SendStringToExecute("REGENALL ", true, false, true);
            acDoc.Editor.WriteMessage($"\n[SetVP] 已在 {viewportIds.Count} 个视口中覆盖 {targetLayers.Count} 个图层颜色。\n");
        }

        // 应用后立即允许还原
        _btnRestore.Enabled = true;
    }

    private void BtnRestore_Click(object? sender, EventArgs e)
    {
        ColorOverrideService.RestoreOriginalColors();
        _btnRestore.Enabled = false;  // 还原后不能再还原

        // 刷新显示 (REGENALL)
        var acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        if (acDoc != null)
        {
            acDoc.SendStringToExecute("REGENALL ", true, false, true);
            acDoc.Editor.WriteMessage("\n[SetVP] 已还原原始图层颜色。\n");
        }
    }

    /// <summary>
    /// 关闭已打开的窗口（如果存在）
    /// 由 SetVPExtApp.Terminate 调用
    /// </summary>
    public static void CloseIfOpen()
    {
        if (s_instance != null && !s_instance.IsDisposed)
        {
            try { s_instance.Close(); } catch { }
            s_instance = null;
        }
    }
}
