using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using MapLinker.Objects;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game;
using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using System.Collections.Generic;

namespace MapLinker
{
    public class MapLinker : IDalamudPlugin
    {
        public string Name => "MapLinker";
        public PluginUi Gui { get; private set; }
        public DalamudPluginInterface Interface { get; private set; }
        public CommandManager CommandManager { get; private set; }
        public DataManager DataManager { get; private set; }
        public ClientState ClientState { get; private set; }
        public Framework Framework { get; private set; }
        public ChatGui ChatGui { get; private set; }
        public GameGui GameGui { get; private set; }

        public Configuration Config { get; private set; }
        public PlayerCharacter LocalPlayer => ClientState.LocalPlayer;
        public bool IsLoggedIn => LocalPlayer != null;
        public bool IsInHomeWorld => LocalPlayer?.CurrentWorld == LocalPlayer?.HomeWorld;

        public Lumina.Excel.ExcelSheet<Aetheryte> Aetherytes = null;
        public Lumina.Excel.ExcelSheet<MapMarker> AetherytesMap = null;

        public void Dispose()
        {
            ChatGui.ChatMessage -= Chat_OnChatMessage;
            CommandManager.RemoveHandler("/maplink");
            Gui?.Dispose();
            Interface?.Dispose();
        }

        public MapLinker(
            DalamudPluginInterface pluginInterface,
            ChatGui chat,
            CommandManager commands,
            DataManager data,
            ClientState clientState,
            Framework framework,
            GameGui gameGui)
        {
            Interface = pluginInterface;
            ClientState = clientState;
            Framework = framework;
            CommandManager = commands;
            DataManager = data;
            ChatGui = chat;
            GameGui = gameGui;
            Aetherytes = DataManager.GetExcelSheet<Aetheryte>(ClientState.ClientLanguage);
            AetherytesMap = DataManager.GetExcelSheet<MapMarker>(ClientState.ClientLanguage);
            Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(pluginInterface);
            CommandManager.AddHandler("/maplink", new CommandInfo(CommandHandler)
            {
                HelpMessage = "/maplink - open the maplink panel."
            });
            Gui = new PluginUi(this);
            ChatGui.ChatMessage += Chat_OnChatMessage;
        }
        public void CommandHandler(string command, string arguments)
        {
            var args = arguments.Trim().Replace("\"", string.Empty);
            string[] argsArray = args.Split(" ");

            if (argsArray.Length == 2)
            {
                int listIndex;
                List<MapLinkMessage> mapList = Config.MapLinkMessageList;
                if (Config.SortDesc)
                {
                    mapList = mapList.OrderByDescending(mlm => mlm.RecordTime).ToList();
                }
                else
                {
                    mapList = mapList.OrderBy(mlm => mlm.RecordTime).ToList();
                }

                // Convert to zero-based numbering
                if (argsArray[1].Equals("first", StringComparison.OrdinalIgnoreCase))
                {
                    listIndex = 0;
                }
                else if (argsArray[1].Equals("last", StringComparison.OrdinalIgnoreCase))
                {
                    listIndex = mapList.Count - 1;
                }
                else if (int.TryParse(argsArray[1], out listIndex))
                {
                    if (listIndex <= 0 || listIndex > mapList.Count)
                    {
                        listIndex = -1;
                    }
                    else
                    {
                        listIndex--;
                    }
                }
                else
                {
                    listIndex = -1;
                }

                if (listIndex < 0 || listIndex > mapList.Count)
                {
                    Gui.ConfigWindow.Visible = !Gui.ConfigWindow.Visible;
                    return;
                }

                if (argsArray[0].Equals("use", StringComparison.OrdinalIgnoreCase))
                {
                    PlaceMapMarker(mapList[listIndex]);
                    TeleportToAetheryte(mapList[listIndex]);
                }
                else if (argsArray[0].Equals("go", StringComparison.OrdinalIgnoreCase))
                {
                    TeleportToAetheryte(mapList[listIndex]);
                }
                else if (argsArray[0].Equals("map", StringComparison.OrdinalIgnoreCase))
                {
                    PlaceMapMarker(mapList[listIndex]);
                }
            }
            else
            {
                Gui.ConfigWindow.Visible = !Gui.ConfigWindow.Visible;
                return;
            }
        }
        public void Log(string message)
        {
            if (!Config.PrintMessage) return;
            var msg = $"[{Name}] {message}";
            PluginLog.Log(msg);
            ChatGui.Print(msg);
        }
        public void LogError(string message)
        {
            if (!Config.PrintError) return;
            var msg = $"[{Name}] {message}";
            PluginLog.LogError(msg);
            ChatGui.PrintError(msg);
        }
        private int ConvertMapCoordinateToRawPosition(float pos, float scale)
        {
            float num = scale / 100f;
            return (int)((float)((pos - 1.0) * num / 41.0 * 2048.0 - 1024.0) / num * 1000f);
        }
        private float ConvertRawPositionToMapCoordinate(int pos, float scale)
        {
            float num = scale / 100f;
            return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
        }

        private float ConvertMapMarkerToMapCoordinate(int pos, float scale)
        {
            float num = scale / 100f;
            var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
            return ConvertRawPositionToMapCoordinate(rawPosition, scale);
        }

        public string GetNearestAetheryte(MapLinkMessage maplinkMessage)
        {
            string aetheryteName = "";
            double distance = 0;
            foreach (var data in Aetherytes)
            {
                if (!data.IsAetheryte) continue;
                if (data.Territory.Value == null) continue;
                if (data.PlaceName.Value == null) continue;
                var scale = maplinkMessage.Scale;
                if (data.Territory.Value.RowId == maplinkMessage.TerritoryId)
                {
                    var mapMarker = AetherytesMap.GetRows().Where(m => (m.DataType == 3 && m.DataKey == data.RowId)).FirstOrDefault();
                    if (mapMarker == null)
                    {
                        LogError($"Cannot find aetherytes position for {maplinkMessage.PlaceName}#{data.PlaceName.Value.Name}");
                        continue;
                    }
                    var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, scale);
                    var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, scale);
                    Log($"Aetheryte: {data.PlaceName.Value.Name} ({AethersX} ,{AethersY})");
                    double temp_distance = Math.Pow(AethersX - maplinkMessage.X, 2) + Math.Pow(AethersY - maplinkMessage.Y, 2);
                    if (aetheryteName == "" || temp_distance < distance)
                    {
                        distance = temp_distance;
                        aetheryteName = data.PlaceName.Value.Name;
                    }
                }
            }
            return aetheryteName;
        }

        private void Chat_OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (!Config.Recording) return;
            bool hasMapLink = false;
            float coordX = 0;
            float coordY = 0;
            float scale = 100;
            MapLinkPayload maplinkPayload = null;
            foreach (var payload in message.Payloads)
            {
                if (payload is MapLinkPayload mapLinkload)
                {
                    maplinkPayload = mapLinkload;
                    hasMapLink = true;
                    // float fudge = 0.05f;
                    scale = mapLinkload.TerritoryType.Map.Value.SizeFactor;
                    // coordX = ConvertRawPositionToMapCoordinate(mapLinkload.RawX, scale) - fudge;
                    // coordY = ConvertRawPositionToMapCoordinate(mapLinkload.RawY, scale) - fudge;
                    coordX = mapLinkload.XCoord;
                    coordY = mapLinkload.YCoord;
                    Log($"TerritoryId: {mapLinkload.TerritoryType.RowId} {mapLinkload.PlaceName} ({coordX} ,{coordY})");
                }
            }
            string messageText = message.TextValue;
            if (hasMapLink)
            {
                var newMapLinkMessage = new MapLinkMessage(
                        (ushort)type,
                        sender.TextValue,
                        messageText,
                        coordX,
                        coordY,
                        scale,
                        maplinkPayload.TerritoryType.RowId,
                        maplinkPayload.PlaceName,
                        DateTime.Now
                    );
                bool filteredOut = false;
                if (sender.TextValue.ToLower() == "sonar")
                    filteredOut = true;
                bool alreadyInList = Config.MapLinkMessageList.Any(w => {
                    bool sameText = w.Text == newMapLinkMessage.Text;
                    var timeoutMin = new TimeSpan(0, Config.FilterDupTimeout, 0);
                    if (newMapLinkMessage.RecordTime < w.RecordTime + timeoutMin)
                    {
                        bool sameX = (int)(w.X * 10) == (int)(newMapLinkMessage.X * 10);
                        bool sameY = (int)(w.Y * 10) == (int)(newMapLinkMessage.Y * 10);
                        bool sameTerritory = w.TerritoryId == newMapLinkMessage.TerritoryId;
                        return sameTerritory && sameX && sameY;
                    }
                    return sameText;
                });
                if (Config.FilterDuplicates && alreadyInList) filteredOut = true;
                if (!filteredOut && Config.FilteredChannels.IndexOf((ushort)type) != -1) filteredOut = true;
                if (!filteredOut)
                {
                    Config.MapLinkMessageList.Add(newMapLinkMessage);
                    if (Config.MapLinkMessageList.Count > Config.MaxRecordings)
                    {
                        var tempList = Config.MapLinkMessageList.OrderBy(e => e.RecordTime);
                        Config.MapLinkMessageList.RemoveRange(0, Config.MapLinkMessageList.Count - Config.MaxRecordings);
                        var infoMsg = $"There are too many records, truncated to the latest {Config.MaxRecordings} records";
                        PluginLog.Information(infoMsg);
                    }
                    Config.Save();
                }
            }
        }

        public void TeleportToAetheryte(MapLinkMessage maplinkMessage)
        {
            if (!Config.Teleport) return;
            var aetheryteName = GetNearestAetheryte(maplinkMessage);
            if (aetheryteName != "")
            {
                Log($"Teleporting to {aetheryteName}");
                CommandManager.ProcessCommand($"/tp {aetheryteName}");
            }
            else
            {
                LogError($"Cannot find nearest aetheryte of {maplinkMessage.PlaceName}({maplinkMessage.X}, {maplinkMessage.Y}).");
            }
        }

        public void PlaceMapMarker(MapLinkMessage maplinkMessage)
        {
            Log($"Viewing {maplinkMessage.Text}");
            var map = DataManager.GetExcelSheet<TerritoryType>().GetRow(maplinkMessage.TerritoryId).Map;
            var maplink = new MapLinkPayload(maplinkMessage.TerritoryId, map.Row, maplinkMessage.X, maplinkMessage.Y);
            GameGui.OpenMapWithMapLink(maplink);
        }
    }
}
