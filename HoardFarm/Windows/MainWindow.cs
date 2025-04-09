using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using HoardFarm.IPC;
using HoardFarm.Model;
using ImGuiNET;
using System;
using System.Diagnostics;
using System.Numerics;

namespace HoardFarm.Windows;

public class MainWindow : Window
{
    private readonly Configuration conf = Config;

    public MainWindow()
        : base($"Hoard Farm {P.GetType().Assembly.GetName().Version}###HoardFarm",
               ImGuiWindowFlags.AlwaysAutoResize)
    {
        TitleBarButtons =
        [
            new TitleBarButton
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new Vector2(1.5f, 1),
                ShowTooltip = () =>
                {
                    using (_ = ImRaii.Tooltip())
                        ImGui.Text("打开配置");
                },
                Click = _ => P.ShowConfigWindow()
            },

            new TitleBarButton
            {
                Icon = FontAwesomeIcon.QuestionCircle,
                IconOffset = new Vector2(1.5f, 1),
                ShowTooltip = () =>
                {
                    using (_ = ImRaii.Tooltip())
                        ImGui.Text("打开帮助");
                },
                Click = _ =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/Jukkales/HoardFarm/wiki/How-to-run",
                        UseShellExecute = true
                    });
                }
            },

            new TitleBarButton
            {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new Vector2(1.5f, 1),
                ShowTooltip = () =>
                {
                    using (_ = ImRaii.Tooltip())
                        ImGui.Text("支持我 ♥");
                },
                Click = _ =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://ko-fi.com/jukkales",
                        UseShellExecute = true
                    });
                },
            }
        ];
    }

    public override void Draw()
    {
        using (_ = ImRaii.Disabled(!PluginInstalled(NavmeshIPC.Name)))
        {
            var enabled = HoardService.HoardMode;
            if (ImGui.Checkbox("启用Hoard Farm模式", ref enabled))
                HoardService.HoardMode = enabled;
        }

        if (!PluginInstalled(NavmeshIPC.Name))
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip($"此功能需要安装 {NavmeshIPC.Name}。");
        }

        ImGui.SameLine(230);
        ImGui.Text(HoardService.HoardModeStatus);
        ImGui.Text("在此之后停止：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##stopAfter", ref Config.StopAfter))
            Config.Save();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);

        if (ImGui.Combo("##stopAfterMode", ref Config.StopAfterMode, ["次数", "发现宝藏", "分钟"], 3))
            Config.Save();

        DrawRetainerSettings();

        ImGui.Separator();

        ImGui.BeginGroup();
        ImGui.Text("存档：");
        ImGui.Indent(15);

        if (ImGui.RadioButton("存档 1", ref conf.HoardModeSave, 0))
            Config.Save();

        if (ImGui.RadioButton("存档 2", ref conf.HoardModeSave, 1))
            Config.Save();

        ImGui.Unindent(15);
        ImGui.EndGroup();

        ImGui.SameLine(170);

        ImGui.BeginGroup();
        ImGui.Text("伐木模式：");
        ImGui.Indent(15);
        if (ImGui.RadioButton("效率", ref conf.HoardFarmMode, 0))
            Config.Save();

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("效率模式将搜索并尝试找到每一个宝藏。\n" +
                             ">20%% 的宝藏无法从一开始就到达。\n" +
                             "找到它需要几秒钟的时间。\n" +
                             "此模式还是值得推荐的。");
        }

        if (ImGui.RadioButton("安全", ref conf.HoardFarmMode, 1))
            Config.Save();


        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("安全模式不会搜索宝藏。\n" +
                             "如果无法找到宝藏，它就会立即离开。\n" +
                             "运行更安全、更快，但你会错过很多宝藏。\n" +
                             "总体来说需要更长的时间。");
        }

        ImGui.Unindent(15);
        ImGui.EndGroup();

        ImGui.Separator();
        ImGui.Text("统计数据：");

        ImGui.BeginGroup();

        ImGui.Text("当前会话");
        ImGui.Text("次数： " + HoardService.SessionRuns);
        var sessionPercent = HoardService.SessionFoundHoards == 0
                                 ? 0
                                 : HoardService.SessionFoundHoards / (double)HoardService.SessionRuns * 100;
        ImGui.Text(
            $"发现： {HoardService.SessionFoundHoards}   ({sessionPercent:0.##} %%)");

        var sessionTimeAverage = HoardService.SessionFoundHoards == 0
                                     ? 0
                                     : HoardService.SessionTime / HoardService.SessionFoundHoards;
        if (sessionTimeAverage > 0)
            ImGui.Text($"时间： {FormatTime(HoardService.SessionTime)}   (Ø {FormatTime(sessionTimeAverage, false)})");
        else
            ImGui.Text("时间： " + FormatTime(HoardService.SessionTime));

        ImGui.EndGroup();
        ImGui.SameLine(170);
        ImGui.BeginGroup();

        ImGui.Text("总览");
        ImGui.Text("次数： " + Config.OverallRuns);
        var overallPercent = Config.OverallRuns == 0 ? 0 : Config.OverallFoundHoards / (double)Config.OverallRuns * 100;
        ImGui.Text(
            $"发现： {Config.OverallFoundHoards}   ({overallPercent:0.##} %%)");

        var overallTimeAverage = Config.OverallFoundHoards == 0 ? 0 : Config.OverallTime / Config.OverallFoundHoards;
        if (overallTimeAverage > 0)
            ImGui.Text($"时间： {FormatTime(Config.OverallTime)}   (Ø {FormatTime(overallTimeAverage, false)})");
        else
            ImGui.Text("时间： " + FormatTime(Config.OverallTime));

        ImGui.EndGroup();
        ImGui.Separator();

        ImGui.Text("进度： " + Achievements.Progress + " / 20000");
        if (Achievements.Progress == 0)
            ImGui.Text("这得花很长时间。相信我");
        else if (overallTimeAverage == 0)
            ImGui.Text("承受更多痛苦。无法计算剩余时间。");
        else
        {
            ImGui.Text(
                $"您至少需要 {FormatRemaining((20000 - Achievements.Progress) * overallTimeAverage)} 的伐木才能完成成就。");
        }

        if (HoardService.HoardModeError != string.Empty)
        {
            ImGui.Separator();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(new Vector4(1, 0, 0, 1), FontAwesomeIcon.ExclamationTriangle.ToIconString());
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "无法运行：\n");
            ImGui.Text(HoardService.HoardModeError);
        }
    }

    private void DrawRetainerSettings()
    {
        var autoRetainer = RetainerApi.Ready && AutoRetainerVersionHighEnough();
        using (_ = ImRaii.Disabled(!autoRetainer))
        {
            if (ImGui.Checkbox("收雇员：", ref Config.DoRetainers))
                Config.Save();
        }

        var hoverText = "如果雇员探险完成，则传送到海都并在运行期间收雇员。";

        if (autoRetainer && !RetainerScv.CanRunRetainer())
            hoverText = "请检查当前的服务器或背包格子。";
        if (!autoRetainer)
            hoverText = "此功能需要安装和配置 AutoRetainer 4.2.6.3 或更高版本。";

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(hoverText);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(170);
        if (ImGui.Combo("##retainerMode", ref Config.RetainerMode,
                        ["如果任何雇员已完成", "如果所有雇员都已完成"], 2))
            Config.Save();
    }

    private static String FormatTime(int seconds, bool withHours = true)
    {
        var timespan = TimeSpan.FromSeconds(seconds);
        return timespan.ToString(withHours ? timespan.Days >= 1 ? @"d\:hh\:mm\:ss" : @"hh\:mm\:ss" : @"mm\:ss");
    }

    private static String FormatRemaining(int seconds)
    {
        var timespan = TimeSpan.FromSeconds(seconds);
        return (timespan.Days >= 1 ? timespan.Days + " 天和 " : "") + timespan.ToString(@"hh\:mm\:ss");
    }
}
