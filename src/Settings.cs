using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CampusClock
{
    public class SettingsPage : Grid
    {
        private AppCore core;
        private StackPanel content;
        private Border previewBox;
        private TextBlock valueFont;
        private TextBlock valueCol;
        private TextBlock valueRow;
        private TextBlock valueOpacity;
        private TextBlock valueBall;
        private TextBlock valueExpand;
        private TextBlock valueAnim;
        private List<Chip> clearChips = new List<Chip>();

        public event EventHandler ImportRequested;

        public SettingsPage(AppCore core)
        {
            this.core = core;
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Padding = new Thickness(0, 0, 8, 0);
            content = new StackPanel();
            sv.Content = content;
            Children.Add(sv);
            Build();
        }

        private StackPanel AddSection(string title, string subtitle)
        {
            TextBlock t = Ui.Text(title, 15, Palette.TextPrimary, FontWeights.SemiBold);
            t.Margin = new Thickness(0, content.Children.Count == 0 ? 0 : 18, 0, 0);
            content.Children.Add(t);
            if (subtitle != null && subtitle.Length > 0)
            {
                TextBlock s = Ui.Text(subtitle, 12, Palette.TextMuted);
                s.Margin = new Thickness(0, 4, 0, 8);
                content.Children.Add(s);
            }
            StackPanel card = new StackPanel();
            Border b = Ui.Card(card, 12, Palette.Surface, Palette.BorderSoft, new Thickness(16, 6, 16, 6));
            content.Children.Add(b);
            return card;
        }

        private void AddRow(StackPanel parent, string title, string desc, FrameworkElement control)
        {
            parent.Children.Add(Ui.Divider());
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[1].Width = GridLength.Auto;
            g.MinHeight = 44;
            StackPanel left = new StackPanel();
            left.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(Ui.Text(title, 13.5, Palette.TextPrimary));
            if (desc != null && desc.Length > 0)
            {
                TextBlock d = Ui.Text(desc, 11.5, Palette.TextMuted, FontWeights.Normal, true);
                d.Margin = new Thickness(0, 3, 12, 0);
                d.MaxWidth = 460;
                left.Children.Add(d);
            }
            g.Children.Add(left);
            if (control != null)
            {
                control.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                g.Children.Add(control);
            }
            parent.Children.Add(g);
        }

        private StackPanel SliderGroup(MiniSlider slider, TextBlock valueLabel)
        {
            StackPanel sp = new StackPanel();
            sp.Width = 230;
            Grid head = new Grid();
            head.Children.Add(Ui.Text("", 1, Palette.TextMuted));
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            valueLabel.Foreground = Palette.Br(Palette.Accent);
            valueLabel.FontSize = 12.5;
            head.Children.Add(valueLabel);
            slider.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(head);
            sp.Children.Add(slider);
            return sp;
        }

        public void Build()
        {
            content.Children.Clear();
            clearChips.Clear();

            // ---------- 显示设置 ----------
            StackPanel display = AddSection("显示设置", "课表纵轴按真实时间等比排列，这里可调整字号、时间轴缩放与列宽");

            valueFont = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider fontSlider = new MiniSlider(10, 24, core.Config.FontSize, 1);
            fontSlider.ValueChanged += delegate
            {
                core.Config.FontSize = (int)fontSlider.Value;
                valueFont.Text = ((int)fontSlider.Value).ToString(CultureInfo.InvariantCulture) + " px";
                Apply();
            };
            valueFont.Text = core.Config.FontSize.ToString(CultureInfo.InvariantCulture) + " px";
            AddRow(display, "字体大小", "课表里课程名称、时间与地点的文字大小", SliderGroup(fontSlider, valueFont));

            valueCol = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider colSlider = new MiniSlider(70, 200, core.Config.CellWidth, 2);
            colSlider.ValueChanged += delegate
            {
                core.Config.CellWidth = (int)colSlider.Value;
                valueCol.Text = ((int)colSlider.Value).ToString(CultureInfo.InvariantCulture) + " px";
                Apply();
            };
            valueCol.Text = core.Config.CellWidth.ToString(CultureInfo.InvariantCulture) + " px";
            AddRow(display, "单元格宽度", "每一列（每一天）的宽度", SliderGroup(colSlider, valueCol));

            valueRow = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider rowSlider = new MiniSlider(28, 200, core.Config.PxPerHour, 2);
            rowSlider.ValueChanged += delegate
            {
                core.Config.PxPerHour = (int)rowSlider.Value;
                valueRow.Text = ((int)rowSlider.Value).ToString(CultureInfo.InvariantCulture) + " px/小时";
                Apply();
            };
            valueRow.Text = core.Config.PxPerHour.ToString(CultureInfo.InvariantCulture) + " px/小时";
            AddRow(display, "时间轴缩放", "纵轴每小时占多少像素；课程按真实时长等比占高，1 小时的课就是 1 格",
                SliderGroup(rowSlider, valueRow));

            valueOpacity = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider opacitySlider = new MiniSlider(10, 60, core.Config.CardOpacity * 100, 2);
            opacitySlider.ValueChanged += delegate
            {
                core.Config.CardOpacity = opacitySlider.Value / 100.0;
                valueOpacity.Text = ((int)opacitySlider.Value).ToString(CultureInfo.InvariantCulture) + " %";
                Apply();
            };
            valueOpacity.Text = ((int)Math.Round(core.Config.CardOpacity * 100)).ToString(CultureInfo.InvariantCulture) + " %";
            AddRow(display, "课程卡片浓度", "课程色块的不透明度", SliderGroup(opacitySlider, valueOpacity));

            Switch weekend = new Switch(core.Config.ShowWeekend);
            weekend.Toggled += delegate
            {
                core.Config.ShowWeekend = weekend.IsOn;
                Apply();
            };
            AddRow(display, "显示周末", "关闭时只显示周一到周五", weekend);

            Switch nowMarker = new Switch(core.Config.ShowNowMarker);
            nowMarker.Toggled += delegate
            {
                core.Config.ShowNowMarker = nowMarker.IsOn;
                Apply();
            };
            AddRow(display, "当前位置提示", "在当天正在上课的时段上高亮", nowMarker);

            StackPanel swatches = new StackPanel();
            swatches.Orientation = Orientation.Horizontal;
            string[] accents = new string[] { "#6D8DFF", "#4CC38A", "#E5A34A", "#E5738B", "#A48CF0", "#4BB8C8" };
            for (int i = 0; i < accents.Length; i++)
            {
                string hex = accents[i];
                Border dot = new Border();
                dot.Width = 26;
                dot.Height = 26;
                dot.CornerRadius = new CornerRadius(13);
                dot.Background = Palette.Br(Palette.Hex(hex));
                dot.Margin = new Thickness(0, 0, 10, 0);
                dot.Cursor = Cursors.Hand;
                if (string.Equals(hex, core.Config.Accent, StringComparison.OrdinalIgnoreCase))
                {
                    dot.BorderBrush = Palette.Br(Colors.White);
                    dot.BorderThickness = new Thickness(2);
                }
                dot.MouseLeftButtonUp += delegate
                {
                    core.Config.Accent = hex;
                    Palette.SetAccent(hex);
                    core.SaveConfig();
                    Build();
                };
                swatches.Children.Add(dot);
            }
            AddRow(display, "强调色", "高亮、选中状态与悬浮球的主色", swatches);

            previewBox = new Border();
            previewBox.Height = 210;
            previewBox.Margin = new Thickness(0, 10, 0, 6);
            previewBox.CornerRadius = new CornerRadius(10);
            previewBox.Background = Palette.Br(Palette.Hex("#101317"));
            previewBox.BorderBrush = Palette.Br(Palette.BorderSoft);
            previewBox.BorderThickness = new Thickness(1);
            previewBox.ClipToBounds = true;
            previewBox.Child = BuildPreview();
            AddRow(display, "预览", "当前显示的等比例缩略效果", null);
            content.Children.Add(previewBox);

            // ---------- 悬浮球 ----------
            StackPanel ballSec = AddSection("桌面悬浮球", "鼠标悬停时展开，移开自动收起");

            Switch ballOn = new Switch(core.Config.BallEnabled);
            ballOn.Toggled += delegate
            {
                core.Config.BallEnabled = ballOn.IsOn;
                core.SaveConfig();
            };
            AddRow(ballSec, "启用悬浮球", "在桌面上常驻一个圆形悬浮球", ballOn);

            valueBall = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider ballSize = new MiniSlider(44, 110, core.Config.BallSize, 2);
            ballSize.ValueChanged += delegate
            {
                core.Config.BallSize = (int)ballSize.Value;
                valueBall.Text = ((int)ballSize.Value).ToString(CultureInfo.InvariantCulture) + " px";
                Apply();
            };
            valueBall.Text = core.Config.BallSize.ToString(CultureInfo.InvariantCulture) + " px";
            AddRow(ballSec, "悬浮球尺寸", "收起状态下的高度；宽度按 1.6 倍加宽，左右两块各占一半",
                SliderGroup(ballSize, valueBall));

            valueExpand = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider expandSize = new MiniSlider(8, 45, core.Config.ExpandAreaPercent, 1);
            expandSize.ValueChanged += delegate
            {
                core.Config.ExpandAreaPercent = (int)expandSize.Value;
                valueExpand.Text = ((int)expandSize.Value).ToString(CultureInfo.InvariantCulture) + " %";
                Apply();
            };
            valueExpand.Text = core.Config.ExpandAreaPercent.ToString(CultureInfo.InvariantCulture) + " %";
            AddRow(ballSec, "展开尺寸", "展开后的正方形占桌面面积的百分比（近似 1/4 ~ 1/5 桌面）",
                SliderGroup(expandSize, valueExpand));

            valueAnim = Ui.Text("", 12.5, Palette.Accent);
            MiniSlider anim = new MiniSlider(0, 400, core.Config.AnimationMs, 20);
            anim.ValueChanged += delegate
            {
                core.Config.AnimationMs = (int)anim.Value;
                valueAnim.Text = ((int)anim.Value).ToString(CultureInfo.InvariantCulture) + " ms";
                core.SaveConfig();
            };
            valueAnim.Text = core.Config.AnimationMs.ToString(CultureInfo.InvariantCulture) + " ms";
            AddRow(ballSec, "展开动画", "展开 / 收起的动画时长", SliderGroup(anim, valueAnim));

            Switch topmost = new Switch(core.Config.BallTopmost);
            topmost.Toggled += delegate
            {
                core.Config.BallTopmost = topmost.IsOn;
                core.SaveConfig();
            };
            AddRow(ballSec, "始终置顶", "悬浮球显示在所有窗口最上层", topmost);

            Switch pin = new Switch(core.Config.BallPinned);
            pin.Toggled += delegate
            {
                core.Config.BallPinned = pin.IsOn;
                core.SaveConfig();
            };
            AddRow(ballSec, "固定展开", "开启后鼠标移开也不会收起，可用悬浮球上的图钉按钮切换", pin);

            TextBtn resetPos = TextBtn.Ghost("复位到右下角");
            resetPos.HorizontalAlignment = HorizontalAlignment.Right;
            resetPos.Clicked += delegate
            {
                core.Config.BallLeft = int.MinValue;
                core.Config.BallTop = int.MinValue;
                core.SaveConfig();
            };
            AddRow(ballSec, "悬浮球位置", "拖动悬浮球可移动位置；此处恢复到屏幕右下角", resetPos);

            // ---------- 作业 ----------
            StackPanel hw = AddSection("作业记录", "选择需要记录作业的课程，并设置清空时机");

            StackPanel chipRow = new StackPanel();
            chipRow.Orientation = Orientation.Horizontal;
            string[] modes = new string[] { "PerSession", "PerDay", "Manual" };
            string[] labels = new string[] { "每次课后", "每天首次课后", "手动" };
            for (int i = 0; i < modes.Length; i++)
            {
                string mode = modes[i];
                Chip c = new Chip(labels[i]);
                c.Margin = new Thickness(0, 0, 8, 0);
                c.IsSelected = core.Config.ClearMode == mode;
                c.Toggled += delegate
                {
                    core.Config.ClearMode = mode;
                    core.SaveConfig();
                    Build();
                };
                chipRow.Children.Add(c);
                clearChips.Add(c);
            }
            AddRow(hw, "清空时机", "课程上完之后自动清空该课程作业内容，等待你重新填写", chipRow);

            TextBlock note = Ui.Text("说明：每次课后清空表示该课程任意一节上完后就清空；" +
                "每天首次课后清空表示同一天只清空一次。首次启用时以当前最近一次课为基准，不会立刻清空已有内容。",
                11.5, Palette.TextMuted, FontWeights.Normal, true);
            note.Margin = new Thickness(0, 12, 0, 6);
            note.MaxWidth = 720;
            hw.Children.Add(note);

            // ---------- 数据 ----------
            StackPanel data = AddSection("课表与数据", "导入的 .ics 会复制到应用目录，原文件可以随意移动");
            string info;
            if (core.Doc != null && core.Doc.Rules.Count > 0)
            {
                info = "课程 " + core.Doc.Courses().Count.ToString(CultureInfo.InvariantCulture) +
                    " 门 · 重复事件 " + core.Doc.EventCount.ToString(CultureInfo.InvariantCulture) +
                    " 条 · 学期 " + Fmt.Date(core.Doc.FirstDate()) + " ~ " + Fmt.Date(core.Doc.LastDate());
            }
            else
            {
                info = "尚未导入课表";
            }
            AddRow(data, "当前课表", info, null);
            AddRow(data, "课表文件", Paths.IcsFile, null);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            TextBtn import = TextBtn.Primary("导入 .ics", Palette.Accent);
            import.Margin = new Thickness(0, 0, 8, 0);
            import.Clicked += delegate
            {
                if (ImportRequested != null) ImportRequested(this, EventArgs.Empty);
            };
            TextBtn openData = TextBtn.Ghost("打开数据目录");
            openData.Margin = new Thickness(0, 0, 8, 0);
            openData.Clicked += delegate { OpenPath(Paths.DataDir); };
            TextBtn openLog = TextBtn.Ghost("查看日志");
            openLog.Clicked += delegate { OpenPath(Paths.LogFile); };
            buttons.Children.Add(import);
            buttons.Children.Add(openData);
            buttons.Children.Add(openLog);
            AddRow(data, "操作", "数据目录：" + Paths.DataDir, buttons);

            if (core.Doc != null && core.Doc.Warnings.Count > 0)
            {
                TextBlock warn = Ui.Text("解析提示：" + core.Doc.Warnings[0], 11.5, Palette.Warn, FontWeights.Normal, true);
                warn.Margin = new Thickness(0, 12, 0, 6);
                data.Children.Add(warn);
            }

            TextBlock about = Ui.Text("CampusClock 1.0.2 · 本地运行，无需登录 · 数据仅保存在应用目录中",
                11.5, Palette.TextMuted);
            about.Margin = new Thickness(0, 20, 0, 10);
            content.Children.Add(about);
        }

        private FrameworkElement BuildPreview()
        {
            DateTime weekStart = Schedule.WeekStart(DateTime.Today);
            FrameworkElement grid = TimetableRenderer.Build(core.Schedule, core.Config, weekStart, true, 640, 200);
            grid.HorizontalAlignment = HorizontalAlignment.Stretch;
            grid.VerticalAlignment = VerticalAlignment.Stretch;
            return grid;
        }

        public void RefreshPreview()
        {
            if (previewBox != null) previewBox.Child = BuildPreview();
        }

        private void Apply()
        {
            core.Config.Normalize();
            core.SaveConfig();
            RefreshPreview();
        }

        public static void OpenPath(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                Log.Warn("打开路径失败：" + ex.Message);
            }
        }
    }
}
