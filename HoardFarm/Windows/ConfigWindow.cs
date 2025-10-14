using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using HoardFarm.Data;

namespace HoardFarm.Windows;

public class ConfigWindow() : Window(Strings.ConfigWindow_Title, ImGuiWindowFlags.AlwaysAutoResize)
{
    public override void Draw()
    {
        ImGui.Text(Strings.ConfigWindow_GreetingText);
        ImGui.Text(Strings.ConfigWindow_SupportMe);
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

        if (ImGui.Button(Strings.ConfigWindow_SupportKofi))
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/jukkales",
                UseShellExecute = true
            });

        ImGui.PopStyleColor(3);
        if (ImGui.Button("Want to help with localization?"))
            Process.Start(new ProcessStartInfo
                              { FileName = "https://crowdin.com/project/hoardfarm", UseShellExecute = true });
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button(Strings.ConfigWindow_ResetStatistics))
        {
            Config.OverallRuns = 0;
            Config.OverallFoundHoards = 0;
            Config.OverallTime = 0;
            Config.Save();
        }
        
        ImGui.Text(Strings.ConfigWindow_Language);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        var languages = GetLanguages();  
        var lang = languages.Find(pair => pair.Key == Config.Language).Value;
        if (ImGui.BeginCombo("##language", lang))
        {
            foreach (var language in languages)
            {
                var selected = language.Key == Config.Language;
                if (ImGui.Selectable(language.Value, selected))
                {
                    Config.Language = language.Key;
                    Config.Save();
                    if (language.Key == "")
                    {
                        CultureInfo.DefaultThreadCurrentUICulture = ClientState.ClientLanguage switch
                        {
                            ClientLanguage.French => CultureInfo.GetCultureInfo("fr"),
                            ClientLanguage.German => CultureInfo.GetCultureInfo("de"),
                            ClientLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
                            _ => CultureInfo.GetCultureInfo("en")
                        };
                    }
                    else
                    {
                        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(Config.Language);
                    }
                }
            }
            ImGui.EndCombo();
        }
        
        if (ImGui.Checkbox(Strings.ConfigWindow_OpenHoardfarmOverlay, ref Config.ShowOverlay))
        {
            Config.Save();
        }
        
        if (ImGui.Checkbox(Strings.ConfigWindow_DisableStatisticGathering, ref Config.DisableStatisticCollection))
        {
            Config.Save();
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigWindow_DisableStatisticGathering_Help);
        }
        
        if (ImGui.Checkbox(Strings.ConfigWindow_SlowMode, ref Config.ParanoidMode))
        {
            Config.Save();
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigWindow_SlowMode_Help);
        }

        if (Config.ParanoidMode)
        {
            ImGui.Indent();
            ImGui.Text(Strings.ConfigWindow_WaitBetween);
            ImGui.SetNextItemWidth(80);
            if (ImGui.SliderInt("###MinWait", ref Config.MinWaitTime, 0, 10, Strings.ConfigWindow_Seconds))
            {
                if (Config.MinWaitTime > Config.MaxWaitTime) Config.MinWaitTime = Config.MaxWaitTime;
                Config.Save();
            }
            ImGui.SameLine();
            ImGui.Text(Strings.ConfigWindow_And);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            if (ImGui.SliderInt("###MaxWait", ref Config.MaxWaitTime, 0, 20, Strings.ConfigWindow_Seconds))
            {
                if (Config.MaxWaitTime < Config.MinWaitTime) Config.MaxWaitTime = Config.MinWaitTime;
                Config.Save();
            }
            ImGui.Unindent();
        }
    }
    
    private List<KeyValuePair<String, String>> GetLanguages()
    {
        return new List<KeyValuePair<string, string>>
        {
            new[]
            {
                new KeyValuePair<string, string>("", Strings.ConfigWindow_Language_default),
                new KeyValuePair<string, string>("de", Strings.ConfigWindow_Language_de),
                new KeyValuePair<string, string>("en", Strings.ConfigWindow_Language_en),
                new KeyValuePair<string, string>("fr", Strings.ConfigWindow_Language_fr),
                new KeyValuePair<string, string>("ja", Strings.ConfigWindow_Language_ja),
                new KeyValuePair<string, string>("zh", Strings.ConfigWindow_Language_zh),
                new KeyValuePair<string, string>("ko", Strings.ConfigWindow_Language_ko),
            }
        };  
    }
}
