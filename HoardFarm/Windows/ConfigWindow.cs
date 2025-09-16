using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace HoardFarm.Windows;

public class ConfigWindow() : Window("Hoard Farm 配置", ImGuiWindowFlags.AlwaysAutoResize)
{
    public override void Draw()
    {
        ImGui.Text("实际上这里不需要配置太多。");
        ImGui.Text("想支持我吗？请我喝杯咖啡！");
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

        if (ImGui.Button("Ko-fi 支持"))
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/jukkales",
                UseShellExecute = true
            });

        ImGui.PopStyleColor(3);
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("重置统计数据"))
        {
            Config.OverallRuns = 0;
            Config.OverallFoundHoards = 0;
            Config.OverallTime = 0;
            Config.Save();
        }

        if (ImGui.Checkbox("显示“打开 Hoardfarm”悬浮窗", ref Config.ShowOverlay))
        {
            Config.Save();
        }
        if (ImGui.Checkbox("偏执模式", ref Config.ParanoidMode))
        {
            Config.Save();
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("偏执模式将等待几秒钟后再重新排队。\n" +
                             "这并不是必需的，但会让一些人感觉更好。");
        }
    }
}
