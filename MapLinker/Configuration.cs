using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using MapLinker.Objects;

namespace MapLinker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public MapLinkerLanguage MapLinkerLanguage = MapLinkerLanguage.Client;
        public bool ShowTooltips = true;
        public bool UseFloatingWindow;
        public List<MapLinkMessage> MapLinkMessageList = new List<MapLinkMessage>();

        public bool Recording = true;
        public bool Coord = true;
        public bool Teleport = true;
        public bool PrintMessage = false;
        public bool PrintError = true;

        #region Init and Save

        [NonSerialized] private DalamudPluginInterface _pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
#if DEBUG
            PrintMessage = true;
#endif
            _pluginInterface = pluginInterface;
        }

        public void Save()
        {
            _pluginInterface.SavePluginConfig(this);
        }

        #endregion
    }
}