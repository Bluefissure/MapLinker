using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using MapLinker.Objects;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Logging;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Dalamud.IoC;
using Lumina.Excel.Sheets;

namespace MapLinker
{
    public class MapLinker : IDalamudPlugin
    {
        public string Name => "MapLinker";
        public PluginUi Gui { get; private set; }
        public static IDalamudPluginInterface Interface { get; private set; }
        public ICommandManager CommandManager { get; private set; }
        public IDataManager DataManager { get; private set; }
        public IClientState ClientState { get; private set; }
        public ITargetManager TargetManager { get; private set; }
        public IFramework Framework { get; private set; }
        public IChatGui ChatGui { get; private set; }
        public IGameGui GameGui { get; private set; }

        public static IPluginLog PluginLog { get; private set; }

        public Configuration Config { get; private set; }
        public IPlayerCharacter LocalPlayer => ClientState.LocalPlayer;
        public bool IsLoggedIn => LocalPlayer != null;
        //public bool IsInHomeWorld => LocalPlayer?.CurrentWorld == LocalPlayer?.HomeWorld;

        public static object PluginInterface { get; internal set; }

        public Lumina.Excel.ExcelSheet<Aetheryte> Aetherytes = null;
        public Lumina.Excel.SubrowExcelSheet<MapMarker> AetherytesMap = null;

        public void Dispose()
        {
            ChatGui.ChatMessage -= Chat_OnChatMessage;
            CommandManager.RemoveHandler("/maplink");
            Gui?.Dispose();
        }

        public MapLinker(
            IDalamudPluginInterface pluginInterface,
            IChatGui chat,
            ICommandManager commands,
            IDataManager data,
            IClientState clientState,
            IFramework framework,
            IGameGui gameGui,
            ITargetManager targetManager,
            IPluginLog pluginLog)
        {
            Interface = pluginInterface;
            ClientState = clientState;
            TargetManager = targetManager;
            Framework = framework;
            CommandManager = commands;
            DataManager = data;
            ChatGui = chat;
            GameGui = gameGui;
            Aetherytes = DataManager.GetExcelSheet<Aetheryte>(ClientState.ClientLanguage);
            AetherytesMap = DataManager.GetSubrowExcelSheet<MapMarker>(ClientState.ClientLanguage);
            Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            CommandManager.AddHandler("/maplink", new CommandInfo(CommandHandler)
            {
                HelpMessage = "/maplink - open the maplink panel."
            });
            Gui = new PluginUi(this);
            PluginLog = pluginLog;
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
            PluginLog.Info(msg);
            ChatGui.Print(msg);
        }
        public void LogError(string message)
        {
            if (!Config.PrintError) return;
            var msg = $"[{Name}] {message}";
            PluginLog.Error(msg);
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

        private double ToMapCoordinate(double val, float scale)
        {
            var c = scale / 100.0;

            val *= c;
            return ((41.0 / c) * ((val + 1024.0) / 2048.0)) + 1;
        }


        public string GetNearestAetheryte(MapLinkMessage maplinkMessage)
        {
            string aetheryteName = "";
            double distance = 0;
            foreach (var data in Aetherytes)
            {
                if (!data.IsAetheryte) continue;
                //if (data.Territory.Value == null) continue;
                //if (data.PlaceName.Value == null) continue;
                var scale = maplinkMessage.Scale;
                if (data.Territory.Value.RowId == maplinkMessage.TerritoryId)
                {
                    Nullable<MapMarker> mapMarker = AetherytesMap.Flatten().FirstOrDefault(m => m.DataType == 3 && m.DataKey.RowId == data.RowId);
                    if (mapMarker == null)
                    {
                        LogError($"Cannot find aetherytes position for {maplinkMessage.PlaceName}#{data.PlaceName.Value.Name}");
                        continue;
                    }
                    var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.Value.X, scale);
                    var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Value.Y, scale);
                    Log($"Aetheryte: {data.PlaceName.Value.Name} ({AethersX} ,{AethersY})");
                    double temp_distance = Math.Pow(AethersX - maplinkMessage.X, 2) + Math.Pow(AethersY - maplinkMessage.Y, 2);
                    if (aetheryteName == "" || temp_distance < distance)
                    {
                        distance = temp_distance;
                        aetheryteName = data.PlaceName.Value.Name.ToString();
                    }
                }
            }
            return aetheryteName;
        }

        public void GetTarget()
        {
            string messageText = "";
            float coordX = 0;
            float coordY = 0;
            float scale = 100;

            var target = TargetManager.Target;
            var territoryType = ClientState.TerritoryType;
            var place = DataManager.GetExcelSheet<Map>(ClientState.ClientLanguage).FirstOrDefault(m => m.TerritoryType.RowId == territoryType);
            var placeName = place.PlaceName.RowId;
            scale = place.SizeFactor;
            var placeNameRow = DataManager.GetExcelSheet<PlaceName>(ClientState.ClientLanguage).GetRow(placeName).Name;
            if (target != null)
            {
                coordX = (float)ToMapCoordinate(target.Position.X, scale);
                coordY = (float)ToMapCoordinate(target.Position.Z, scale);
                messageText += placeNameRow.ToString();
                messageText += " X:" + coordX.ToString("#0.0");
                messageText += " Y:" + coordY.ToString("#0.0");
                var newMapLinkMessage = new MapLinkMessage(
                        (ushort)XivChatType.Debug,
                        target.Name.ToString(),
                        messageText,
                        coordX,
                        coordY,
                        scale,
                        territoryType,
                        placeNameRow.ToString(),
                        DateTime.Now
                    );
                Config.MapLinkMessageList.Add(newMapLinkMessage);
                if (Config.MapLinkMessageList.Count > Config.MaxRecordings)
                {
                    var tempList = Config.MapLinkMessageList.OrderBy(e => e.RecordTime);
                    Config.MapLinkMessageList.RemoveRange(0, Config.MapLinkMessageList.Count - Config.MaxRecordings);
                    var infoMsg = $"There are too many records, truncated to the latest {Config.MaxRecordings} records";
                    PluginLog.Information(infoMsg);
                }
            }
            

        }

        private void Chat_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
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
                    scale = mapLinkload.TerritoryType.Value.Map.Value.SizeFactor;
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
                    if (Config.BringFront)
                    {
                        Native.Impl.Activate();
                    }
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
            var maplink = new MapLinkPayload(maplinkMessage.TerritoryId, map.RowId, maplinkMessage.X, maplinkMessage.Y);
            GameGui.OpenMapWithMapLink(maplink);
        }
    }
}
