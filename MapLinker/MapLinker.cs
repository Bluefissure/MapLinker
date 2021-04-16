using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina;
using System.Text;
using System.Threading.Tasks;
using Lumina.Excel.GeneratedSheets;
using ImGuiScene;
using System.Security.Principal;
using MapLinker.Objects;

namespace MapLinker
{
    public class MapLinker : IDalamudPlugin
    {
        public string Name => "MapLinker";
        public PluginUi Gui { get; private set; }
        public DalamudPluginInterface Interface { get; private set; }
        public Configuration Config { get; private set; }
        public PlayerCharacter LocalPlayer => Interface.ClientState.LocalPlayer;
        public bool IsLoggedIn => LocalPlayer != null;
        public bool IsInHomeWorld => LocalPlayer?.CurrentWorld == LocalPlayer?.HomeWorld;

        public CommandManager CommandManager = null;

        public Lumina.Excel.ExcelSheet<Aetheryte> Aetherytes = null;
        public Lumina.Excel.ExcelSheet<MapMarker> AetherytesMap = null;

        public void Dispose()
        {
            Interface.Framework.Gui.Chat.OnChatMessage -= Chat_OnChatMessage;
            Interface.CommandManager.RemoveHandler("/maplink");
            Gui?.Dispose();
            Interface?.Dispose();
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Interface = pluginInterface;
            CommandManager = pluginInterface.CommandManager;
            Aetherytes = pluginInterface.Data.GetExcelSheet<Aetheryte>(pluginInterface.ClientState.ClientLanguage);
            AetherytesMap = pluginInterface.Data.GetExcelSheet<MapMarker>(pluginInterface.ClientState.ClientLanguage);
            Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(pluginInterface);
            Interface.CommandManager.AddHandler("/maplink", new CommandInfo(CommandHandler)
            {
                HelpMessage = "/maplink - open the maplink panel."
            });
            Gui = new PluginUi(this);
            Interface.Framework.Gui.Chat.OnChatMessage += Chat_OnChatMessage;
        }
        public void CommandHandler(string command, string arguments)
        {
            var args = arguments.Trim().Replace("\"", string.Empty);

            if (string.IsNullOrEmpty(args) || args.Equals("config", StringComparison.OrdinalIgnoreCase))
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
            Interface.Framework.Gui.Chat.Print(msg);
        }
        public void LogError(string message)
        {
            if (!Config.PrintError) return;
            var msg = $"[{Name}] {message}";
            PluginLog.LogError(msg);
            Interface.Framework.Gui.Chat.PrintError(msg);
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
                    float fudge = 0.05f;
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
                    Config.Save();
                }
            }
        }

    }
}
