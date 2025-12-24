using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using HoardFarm.Data;
using HoardFarm.IPC;
using HoardFarm.Model;
using HoardFarm.Tasks;
using HoardFarm.Tasks.TaskGroups;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace HoardFarm.Service;

public class HoardFarmService : IDisposable
{
    private readonly string hoardFoundMessage;
    private readonly string noHoardMessage;
    private readonly Dictionary<uint, MapObject> objectPositions = new();
    private readonly string senseHoardMessage;
    private readonly List<uint> visitedObjectIds = [];
    public bool FinishRun;
    private bool hoardAvailable;

    private bool hoardFound;
    private bool hoardModeActive;
    public string HoardModeError = "";

    public string HoardModeStatus = "";
    private Vector3 hoardPosition = Vector3.Zero;
    private bool intuitionUsed;
    private bool? lastHoardCollected;
    private bool? lastHoardFound;
    private DateTime lastTick = DateTime.Now;
    private DateTime? movementEnd;
    private DateTime? movementStart;
    private bool movingToHoard;

    private bool running;
    private DateTime runStarted;
    private bool safetyUsed;
    private bool searchMode;
    public int SessionFoundHoards;
    public int SessionRuns;
    public int SessionTime;

    private ushort? currentTerritoryType;

    private DateTime? timingStart;

    public HoardFarmService()
    {
        hoardFoundMessage = DataManager.GetExcelSheet<LogMessage>().GetRow(7274).Text.ToDalamudString().GetText();
        senseHoardMessage = DataManager.GetExcelSheet<LogMessage>().GetRow(7272).Text.ToDalamudString().GetText();
        noHoardMessage = DataManager.GetExcelSheet<LogMessage>().GetRow(7273).Text.ToDalamudString().GetText();

        ClientState.TerritoryChanged += OnMapChange;

        Framework.Update += OnTick;
    }

    public bool HoardMode
    {
        get => hoardModeActive;
        set
        {
            hoardModeActive = value;
            if (hoardModeActive)
                EnableFarm();
            else
                DisableFarm();
        }
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        ClientState.TerritoryChanged -= OnMapChange;
        Framework.Update -= OnTick;
    }

    private void DisableFarm()
    {
        running = false;
        TaskManager.Abort();
        // GatherData(); Discard - might be inaccurate
        HoardModeStatus = "";
        ChatGui.ChatMessage -= OnChatMessage;
        Reset();

        if (RetainerScv.Running) RetainerScv.FinishProcess();
    }

    private void EnableFarm()
    {
        Reset();
        SessionTime = 0;
        SessionRuns = 0;
        SessionFoundHoards = 0;
        running = true;
        HoardModeStatus = Strings.HoardFarm_Status_Running;
        HoardModeError = "";
        ChatGui.ChatMessage += OnChatMessage;
    }

    private void Reset()
    {
        Config.Save();
        intuitionUsed = false;
        hoardFound = false;
        hoardPosition = Vector3.Zero;
        movingToHoard = false;
        hoardAvailable = false;
        searchMode = false;
        FinishRun = false;
        safetyUsed = false;
        objectPositions.Clear();
        runStarted = DateTime.Now;
    }

    private bool SearchLogic()
    {
        HoardModeStatus = Strings.HoardFarm_Status_Searching;

        if (!TaskManager.IsBusy)
        {
            if (!objectPositions.Where(e => !visitedObjectIds.Contains(e.Value.ObjectId))
                                .Where(e => ChestIDs.Contains(e.Value.DataId))
                                .OrderBy(e => e.Value.Position.Distance(Player.Position))
                                .Select(e => e.Value)
                                .TryGetFirst(out var next))
            {
                if (!objectPositions.Where(e => !visitedObjectIds.Contains(e.Value.ObjectId))
                                    .OrderBy(e => e.Value.Position.Distance(Player.Position))
                                    .Select(e => e.Value)
                                    .TryGetFirst(out next))
                {
                    // We should never reach here normally .. but "never" is still a chance > 0% ;)
                    LeaveDuty(Strings.HoardFarm_Status_Unreachable);
                    return true;
                }
            }

            visitedObjectIds.Add(next!.ObjectId);
            Enqueue(new PathfindTask(next.Position, true), 60 * 1000,
                    string.Format(Strings.HoardFarm_Status_SearchingObject, next.ObjectId));
        }

        FindHoardPosition();
        if (hoardPosition != Vector3.Zero)
        {
            NavmeshIPC.PathStop();
            TaskManager.Abort();
            return true;
        }

        return false;
    }

    private void OnTick(IFramework framework)
    {
        if (!running || DateTime.Now - lastTick < TimeSpan.FromSeconds(1))
            return;

        lastTick = DateTime.Now;

        // Retainer do not increase runtime
        if (RetainerScv.Running)
        {
            HoardModeStatus = Strings.HoardFarm_Status_RetainerRunning;
            return;
        }

        SessionTime++;
        Config.OverallTime++;

        if (!NavmeshIPC.NavIsReady())
        {
            HoardModeStatus = Strings.HoardFarm_Status_WaitingNavmesh;
            return;
        }

        UpdateObjectPositions();
        SafetyChecks();

        if (searchMode && hoardPosition == Vector3.Zero)
        {
            if (!SearchLogic())
                return;
        }

        if (!TaskManager.IsBusy && hoardModeActive)
        {
            if (CheckDone() && !FinishRun)
            {
                FinishRun = true;
                GatherData();
                return;
            }

            if (Player.Territory.Value.RowId == HoHMapId1)
            {
                Error(Strings.HoardFarm_Status_Error_Unprepared);
                return;
            }

            if (!InHoH && !InRubySea && NotBusy() && !KyuseiInteractable())
            {
                HoardModeStatus = Strings.HoardFarm_Status_MoveToHoH;
                Enqueue(new MoveToHoHTask());
                EnqueueWait(1000);
            }

            if (InRubySea && NotBusy() && KyuseiInteractable())
            {
                if (FinishRun)
                {
                    HoardModeStatus = Strings.HoardFarm_Status_Finished;
                    HoardMode = false;
                    return;
                }

                if (CheckRetainer())
                {
                    // Do retainers first
                    return;
                }

                GatherData();
                timingStart = DateTime.Now;
                HoardModeStatus = Strings.HoardFarm_Status_EnteringHoH;
                if (Config.ParanoidMode)
                    EnqueueWait(Random.Shared.Next(Config.MinWaitTime * 1000, Config.MaxWaitTime * 1000));
                Enqueue(new EnterHeavenOnHigh());
            }

            if (InHoH && NotBusy())
            {
                if (!intuitionUsed)
                {
                    if (!CheckMinimalSetup())
                    {
                        Error(
                            Strings.HoardFarm_Status_Error_MinimalSetup);
                        return;
                    }

                    if (CanUsePomander(Pomander.Intuition))
                    {
                        Enqueue(new UsePomanderTask(Pomander.Intuition), "Use Intuition");
                        intuitionUsed = true;
                    }
                }
                else
                {
                    if (hoardAvailable)
                    {
                        lastHoardFound = true;
                        FindHoardPosition();

                        if (hoardPosition != Vector3.Zero)
                        {
                            if (!movingToHoard)
                            {
                                // if (!Concealment)
                                // {
                                //     Enqueue(new UsePomanderTask(Pomander.Concealment, false), "Use Concealment");
                                // }
                                movementStart ??= DateTime.Now;
                                Enqueue(new PathfindTask(hoardPosition, true, 1.5f), 60 * 1000, "Move to Hoard");
                                movingToHoard = true;
                                HoardModeStatus = Strings.HoardFarm_Status_MoveToHoard;
                            }
                        }
                        else
                        {
                            if (Config.HoardFarmMode == 1)
                            {
                                LeaveDuty(Strings.HoardFarm_Status_Leaving);
                                return;
                            }

                            if (!hoardFound)
                            {
                                movementStart = DateTime.Now;
                                Enqueue(new UsePomanderTask(Pomander.Concealment), "Use Concealment");
                                searchMode = true;
                                return;
                            }
                        }

                        if (hoardFound)
                        {
                            LeaveDuty(Strings.HoardFarm_Status_Leaving);
                        }
                    }
                    else
                        LeaveDuty(Strings.HoardFarm_Status_Leaving);
                }
            }
        }
    }

    private void SafetyChecks()
    {
        if (InHoH && intuitionUsed)
        {
            if (DateTime.Now.Subtract(runStarted).TotalSeconds > 130 && !Svc.Condition[ConditionFlag.InCombat])
            {
                TaskManager.Abort();
                NavmeshIPC.PathStop();
                LeaveDuty(Strings.HoardFarm_Status_Timeout);
                return;
            }

            if (IsMoving())
            {
                if (!Concealment)
                {
                    if (CanUsePomander(Pomander.Concealment))
                    {
                        if (EzThrottler.Check("Concealment"))
                        {
                            EzThrottler.Throttle("Concealment", 2000);
                            new UsePomanderTask(Pomander.Concealment, false).Run();
                            return; // start next iteration
                        }
                    }
                    else if (CanUsePomander(Pomander.Safety) && !safetyUsed && EzThrottler.Check("Concealment"))
                    {
                        if (EzThrottler.Check("Safety"))
                        {
                            EzThrottler.Throttle("Safety", 2000);
                            new UsePomanderTask(Pomander.Safety, false).Run();
                            safetyUsed = true;
                            return; // start next iteration
                        }
                    }
                }
            }

            if (Svc.Condition[ConditionFlag.InCombat])
            {
                if (CanUseMagicite() && EzThrottler.Check("Magicite"))
                {
                    EzThrottler.Throttle("Magicite", 6000);
                    new UseMagiciteTask().Run();
                }
            }

            if (Svc.Condition[ConditionFlag.Unconscious]) LeaveDuty(Strings.HoardFarm_Status_PlayerDied);
        }
    }

    private void UpdateObjectPositions()
    {
        foreach (var gameObject in ObjectTable)
            objectPositions.TryAdd(gameObject.EntityId,
                                   new MapObject(gameObject.EntityId, gameObject.BaseId, gameObject.Position));
    }

    private bool CheckRetainer()
    {
        if (Config.DoRetainers
            && RetainerService.CheckRetainersDone(Config.RetainerMode == 1)
            && RetainerScv.CanRunRetainer())
        {
            RetainerScv.StartProcess();
            return true;
        }

        return false;
    }

    private bool CheckMinimalSetup()
    {
        if (!CanUsePomander(Pomander.Intuition)) return false;
        if (CanUsePomander(Pomander.Concealment)) return true;

        return CanUsePomander(Pomander.Safety) && CanUseMagicite();
    }

    private void LeaveDuty(string message)
    {
        HoardModeStatus = message;
        SessionRuns++;
        Config.OverallRuns++;
        Enqueue(new LeaveDutyTask());
    }

    private void Error(string message)
    {
        HoardModeStatus = Strings.HoardFarm_Status_Error;
        HoardModeError = message;
        FinishRun = true;
        Enqueue(new LeaveDutyTask());
    }

    private void FindHoardPosition()
    {
        if (hoardPosition == Vector3.Zero &&
            ObjectTable.TryGetFirst(gameObject => gameObject.BaseId == AccursedHoardId, out var hoard))
            hoardPosition = hoard.Position;
    }

    private bool CheckDone()
    {
        switch (Config.StopAfterMode)
        {
            case 0 when SessionRuns >= Config.StopAfter:
            case 1 when SessionFoundHoards >= Config.StopAfter:
            case 2 when SessionTime >= Config.StopAfter * 60:
                return true;
            default:
                return false;
        }
    }

    private void OnMapChange(ushort territoryType)
    {
        if (territoryType is HoHMapId11 or HoHMapId21)
        {
            Reset();
            HoardModeStatus = Strings.HoardFarm_Status_Waiting;
            currentTerritoryType = territoryType;
        }
    }

    private void OnChatMessage(
        XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (senseHoardMessage.Equals(message.TextValue))
        {
            intuitionUsed = true;
            hoardAvailable = true;
            HoardModeStatus = Strings.HoardFarm_Status_HoardFound;
        }

        if (noHoardMessage.Equals(message.TextValue))
        {
            intuitionUsed = true;
            hoardAvailable = false;
            TaskManager.Abort();
            LeaveDuty(Strings.HoardFarm_Status_NoHoard);
        }

        if (hoardFoundMessage.Equals(message.TextValue))
        {
            hoardFound = true;
            movementEnd = DateTime.Now;
            lastHoardCollected = true;
            SessionFoundHoards++;
            Config.OverallFoundHoards++;
            Achievements.Progress++;
            TaskManager.Abort();
            LeaveDuty(Strings.HoardFarm_Status_Done);
        }
    }

    private void GatherData()
    {
        if (timingStart != null)
        {
            var data = new CollectedData
            {
                Sender = Config.UniqueId!,
                Runtime = (DateTime.Now - timingStart.Value).TotalMilliseconds,
                TerritoryTyp = currentTerritoryType!.Value,
                SafetyMode = Config.HoardFarmMode == 1
            };

            if (lastHoardFound.HasValue) data.HoardFound = lastHoardFound.Value;
            if (lastHoardCollected.HasValue) data.HoardCollected = lastHoardCollected.Value;

            if (movementStart.HasValue && movementEnd.HasValue)
                data.MoveTime = (movementEnd.Value - movementStart.Value).TotalMilliseconds;

            if (data.IsValid())
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented,
                   new JsonSerializerSettings
                   {
                       NullValueHandling = NullValueHandling.Ignore
                   });
                PluginLog.Information("Sending log: " + json);
                Task.Factory.StartNew(async () =>
                {
                    using var client = new HttpClient();
                    try
                    {
                        await client.PostAsync("https://ffxiv.jusrv.de/api/hoardfarm",
                                               new StringContent(json, Encoding.UTF8, "application/json"));
                    }
                    catch (Exception e)
                    {
                        PluginLog.Debug(e, "Failed to send data to server");
                    }
                });
            }
        }


        timingStart = null;
        lastHoardFound = false;
        lastHoardCollected = false;
        movementStart = null;
        movementEnd = null;
    }

    private record MapObject(uint ObjectId, uint DataId, Vector3 Position);
}
