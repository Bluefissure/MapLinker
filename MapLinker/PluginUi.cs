using System;
using MapLinker.Gui;

namespace MapLinker
{
    public class PluginUi : IDisposable
    {
        private readonly MapLinker _plugin;
        public ConfigurationWindow ConfigWindow { get; }

        public PluginUi(MapLinker plugin)
        {
            ConfigWindow = new ConfigurationWindow(plugin);

            _plugin = plugin;
            _plugin.Interface.UiBuilder.OnBuildUi += Draw;
            _plugin.Interface.UiBuilder.OnOpenConfigUi += (sender, args) => ConfigWindow.Visible = true;
        }

        private void Draw()
        {
            ConfigWindow.Draw();
        }

        public void Dispose()
        {
            _plugin.Interface.UiBuilder.OnBuildUi -= Draw;
            _plugin.Interface.UiBuilder.OnOpenConfigUi = null;
        }
    }
}