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
            _plugin.Interface.UiBuilder.Draw += Draw;
            _plugin.Interface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        }

        private void Draw()
        {
            ConfigWindow.Draw();
        }
        private void OnOpenConfigUi()
        {
            ConfigWindow.Visible = true;
        }

        public void Dispose()
        {
            _plugin.Interface.UiBuilder.Draw -= Draw;
            _plugin.Interface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        }
    }
}