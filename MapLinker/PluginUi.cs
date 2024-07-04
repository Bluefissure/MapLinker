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
            MapLinker.Interface.UiBuilder.Draw += Draw;
            MapLinker.Interface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            MapLinker.Interface.UiBuilder.OpenMainUi += OnOpenConfigUi;
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
            MapLinker.Interface.UiBuilder.Draw -= Draw;
            MapLinker.Interface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            MapLinker.Interface.UiBuilder.OpenMainUi -= OnOpenConfigUi;
        }
    }
}