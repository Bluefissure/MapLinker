using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.Text;
using MapLinker.Objects;
using System.Linq;

namespace MapLinker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool ShowTooltips = true;
        public bool FilterDuplicates = false;
        public bool UseFloatingWindow;
        public string UILanguage = "en";
        public List<MapLinkMessage> MapLinkMessageList = new List<MapLinkMessage>();

        public bool Recording = true;
        public bool Teleport = false;
        public bool SortDesc = false;
        public int FilterDupTimeout = 5;
        public int MaxRecordings = 100;
        public float CombatOpacity = 1;
        public bool CombatHide = false;
        public bool CombatClickthru = false;
        public bool PrintMessage = false;
        public bool PrintError = true;
        public bool BringFront = false;
        public bool MessageWrap = false;
        public List<ushort> FilteredChannels = new List<ushort>();
        // public List<ushort> RecordingChannels = new List<ushort> { };


        #region Init and Save


        public void Initialize()
        {
#if DEBUG
            PrintMessage = true;
#endif
        }

        public void Save()
        {
            MapLinker.Interface.SavePluginConfig(this);
        }

        #endregion
    }
}