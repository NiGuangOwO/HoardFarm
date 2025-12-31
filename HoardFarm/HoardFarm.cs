using AutoRetainerAPI;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.Reflection;
using HoardFarm.Data;
using HoardFarm.IPC;
using HoardFarm.Model;
using HoardFarm.Service;
using HoardFarm.Windows;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HoardFarm;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class HoardFarm : IDalamudPlugin
{
    private readonly AchievementService achievementService;
    private readonly AutoRetainerApi autoRetainerApi;
    private readonly ConfigWindow configWindow;
    private readonly DeepDungeonMenuOverlay deepDungeonMenuOverlay;

    private readonly HoardFarmService hoardFarmService;
    private readonly MainWindow mainWindow;
    private readonly RetainerService retainerService;
    public readonly WindowSystem WindowSystem = new("HoardFarm");
    private bool isDev;

    public HoardFarm(IDalamudPluginInterface pluginInterface)
    {
#if !DEBUG
        if (pluginInterface.IsDev || !pluginInterface.SourceRepository.Contains("NiGuangOwO/DalamudPlugins"))
        {
            isDev = true;
            return;
        }
#endif
        pluginInterface?.Create<PluginService>();
        P = this;

        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        DalamudReflector.RegisterOnInstalledPluginsChangedEvents(() =>
        {
            if (PluginInstalled(NavmeshIPC.Name))
                NavmeshIPC.Init();
        });

        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Config.UniqueId == null)
        {
            Config.UniqueId = Guid.NewGuid().ToString();
            Config.Save();
        }

        mainWindow = new MainWindow();
        configWindow = new ConfigWindow();
        deepDungeonMenuOverlay = new DeepDungeonMenuOverlay();

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(deepDungeonMenuOverlay);

        hoardFarmService = new HoardFarmService();
        HoardService = hoardFarmService;

        achievementService = new AchievementService();
        Achievements = achievementService;

        autoRetainerApi = new AutoRetainerApi();
        RetainerApi = autoRetainerApi;

        retainerService = new RetainerService();
        RetainerScv = retainerService;

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += () => OnCommand();
        PluginInterface.UiBuilder.OpenConfigUi += ShowConfigWindow;
        Framework.Update += FrameworkUpdate;

        PluginService.TaskManager = new TaskManager();

        if (Config.Language == "")
        {
            CultureInfo.DefaultThreadCurrentUICulture = ClientState.ClientLanguage switch
            {
                ClientLanguage.French => CultureInfo.GetCultureInfo("fr"),
                ClientLanguage.German => CultureInfo.GetCultureInfo("de"),
                ClientLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
                ClientLanguage.ChineseSimplified => CultureInfo.GetCultureInfo("zh"),
                _ => CultureInfo.GetCultureInfo("en")
            };
        }
        else
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(Config.Language);
        }


        EzCmd.Add("/hoardfarm", (_, args) => OnCommand(args), Strings.HoardFarm_CommandHelp);
    }

    public void Dispose()
    {
        if (isDev)
            return;

        WindowSystem.RemoveAllWindows();
        hoardFarmService.Dispose();

        autoRetainerApi.Dispose();
        retainerService.Dispose();

        Framework.Update -= FrameworkUpdate;
        ECommonsMain.Dispose();
    }


    private void FrameworkUpdate(IFramework framework)
    {
        Tick();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void OnCommand(string? args = null)
    {
        args = args?.Trim().ToLower() ?? "";

        switch (args)
        {
            case "c":
            case "config":
                ShowConfigWindow();
                return;
            case "e":
            case "enable":
                HoardService.HoardMode = true;
                return;
            case "d":
            case "disable":
                if (HoardService.HoardMode)
                    HoardService.FinishRun = true;
                return;
            case "t":
            case "toggle":
                HoardService.HoardMode = !HoardService.HoardMode;
                return;
            default:
                ShowMainWindow();
                break;
        }
    }

    public void ShowConfigWindow()
    {
        configWindow.IsOpen = true;
    }

    public void ShowMainWindow()
    {
        if (!mainWindow.IsOpen)
        {
            Achievements.UpdateProgress();
            mainWindow.IsOpen = true;
        }
    }
}
