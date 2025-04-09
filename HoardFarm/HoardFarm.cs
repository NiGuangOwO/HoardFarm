using AutoRetainerAPI;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.Reflection;
using HoardFarm.IPC;
using HoardFarm.Model;
using HoardFarm.Service;
using HoardFarm.Windows;
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

    public HoardFarm(IDalamudPluginInterface? pluginInterface)
    {
        pluginInterface?.Create<PluginService>();
        P = this;
#if RELEASE
        if (PluginInterface.IsDev)
            return;
#endif
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        DalamudReflector.RegisterOnInstalledPluginsChangedEvents(() =>
        {
            if (PluginInstalled(NavmeshIPC.Name))
                NavmeshIPC.Init();
        });

        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

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


        EzCmd.Add("/hoardfarm", (_, args) => OnCommand(args),
                  "打开Hoard Farm窗口。\n" +
                  "/hoardfarm config | c → 打开配置窗口。\n" +
                  "/hoardfarm enable | e → 启用伐木模式。\n" +
                  "/hoardfarm disable | d → 禁用伐木模式。\n" +
                  "/hoardfarm toggle | t → 切换伐木模式。\n"
        );

        CultureInfo.DefaultThreadCurrentUICulture = ClientState.ClientLanguage switch
        {
            ClientLanguage.French => CultureInfo.GetCultureInfo("fr"),
            ClientLanguage.German => CultureInfo.GetCultureInfo("de"),
            ClientLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
            _ => CultureInfo.GetCultureInfo("en")
        };
    }

    public void Dispose()
    {
#if RELEASE
        if (PluginInterface.IsDev)
            return;
#endif
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
