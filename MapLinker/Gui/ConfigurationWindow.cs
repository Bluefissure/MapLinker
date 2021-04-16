using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using ImGuiNET;
using MapLinker.Objects;

namespace MapLinker.Gui
{
    public class ConfigurationWindow : Window<MapLinker>
    {

        public Configuration Config => Plugin.Config;
        private readonly string[] _languageList;
        private int _selectedLanguage;
        private Localizer _localizer;

        public List<XivChatType> HiddenChatType = new List<XivChatType> {
            XivChatType.None,
            XivChatType.CustomEmote,
            XivChatType.StandardEmote,
            XivChatType.SystemMessage,
            XivChatType.SystemError,
            XivChatType.GatheringSystemMessage,
            XivChatType.ErrorMessage,
            XivChatType.RetainerSale
        };

        public ConfigurationWindow(MapLinker plugin) : base(plugin)
        {
            _languageList = new string[] { "en", "zh" };
            _localizer = new Localizer(Config.UILanguage);
        }

        protected override void DrawUi()
        {
            ImGui.SetNextWindowSize(new Vector2(530, 450), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin($"{Plugin.Name} {_localizer.Localize("Panel")}", ref WindowVisible, ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.End();
                return;
            }
            if (ImGui.BeginTabBar(_localizer.Localize("TabBar")))
            {
                if (ImGui.BeginTabItem(_localizer.Localize("Settings") + "##Tab"))
                {
                    if (ImGui.BeginChild("##SettingsRegion"))
                    {
                        if (ImGui.CollapsingHeader(_localizer.Localize("General Settings"), ImGuiTreeNodeFlags.DefaultOpen))
                            DrawGeneralSettings();
                        if (ImGui.CollapsingHeader(_localizer.Localize("Filters")))
                            DrawFilters();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(_localizer.Localize("Records") + "##Tab"))
                {
                    if (ImGui.BeginChild("##RecordsRegion"))
                    {
                        DrawMaplinks();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        

        private void DrawGeneralSettings()
        {
            if (ImGui.Checkbox(_localizer.Localize("Recording"), ref Config.Recording)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip(_localizer.Localize("Automatically record messages with maplink and retrieve later."));
            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextUnformatted(_localizer.Localize("Tooltips"));
            ImGui.AlignTextToFramePadding();
            ImGui.SameLine();
            if (ImGui.Checkbox("##hideTooltipsOnOff", ref Config.ShowTooltips)) Config.Save();


            if (ImGui.Checkbox(_localizer.Localize("Call /coord to retrieval map links"), ref Config.Coord)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip(_localizer.Localize("Add an option to call /coord  to retrieval maplinks.\n" +
                                 "Make sure you have downloaded ChatCoordinates Plugin."));

            if (ImGui.Checkbox(_localizer.Localize("Call /tp to teleport to the nearest aetheryte"), ref Config.Teleport)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip(_localizer.Localize("Add an option to call /tp to teleport to the nearest aetheryte.\n" +
                                 "Make sure you have downloaded Teleporter Plugin."));
            ImGui.TextUnformatted(_localizer.Localize("Language:"));
            if (Plugin.Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip(_localizer.Localize("Change the UI Language."));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##hideLangSetting", ref _selectedLanguage, _languageList, _languageList.Length))
            {
                Config.UILanguage = _languageList[_selectedLanguage];
                _localizer.Language = Config.UILanguage;
                Config.Save();
            }
            if (ImGui.Checkbox(_localizer.Localize("Print Debug Message"), ref Config.PrintMessage)) Config.Save();
            if (ImGui.Checkbox(_localizer.Localize("Print Error Message"), ref Config.PrintError)) Config.Save();

        }

        private void DrawFilters()
        {
            if (ImGui.Checkbox(_localizer.Localize("Filter out duplicates"), ref Config.FilterDuplicates)) Config.Save();
            ImGui.SameLine();
            if (ImGui.DragInt(_localizer.Localize("Timeout"), ref Config.FilterDupTimeout, 1,1,60)) Config.Save();
            if (Plugin.Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip(_localizer.Localize("Maplink within timeout will be filtered by it's maplink instead of full text."));
            ImGui.Columns(4, "FiltersTable", true);
            foreach (ushort chatType in Enum.GetValues(typeof(XivChatType)))
            {
                if (HiddenChatType.IndexOf((XivChatType)chatType) != -1) continue;
                string chatTypeName = Enum.GetName(typeof(XivChatType), chatType);
                bool checkboxClicked = Config.FilteredChannels.IndexOf(chatType) == -1;
                if (ImGui.Checkbox(_localizer.Localize(chatTypeName) + "##filter", ref checkboxClicked))
                {
                    Config.FilteredChannels = Config.FilteredChannels.Distinct().ToList();
                    if (checkboxClicked)
                    {
                        if (Config.FilteredChannels.IndexOf(chatType) != -1)
                            Config.FilteredChannels.Remove(chatType);
                    }
                    else if (Config.FilteredChannels.IndexOf(chatType) == -1)
                    {
                        Config.FilteredChannels.Add(chatType);
                    }
                    Config.FilteredChannels = Config.FilteredChannels.Distinct().ToList();
                    Config.FilteredChannels.Sort();
                    Config.Save();
                }
                ImGui.NextColumn();
            }
            ImGui.Columns(1);

        }

        private void DrawMaplinks()
        {
            // sender, text, time, view, tp, del
            int columns = 4;
            if (Config.Coord) columns++;
            if (Config.Teleport) columns++;
            if (ImGui.Button(_localizer.Localize("Clear")))
            {
                Config.MapLinkMessageList.Clear();
                Config.Save();
            }
            ImGui.Columns(columns, "Maplinks", true);
            ImGui.Separator();
            ImGui.Text(_localizer.Localize("Sender")); ImGui.NextColumn();
            ImGui.Text(_localizer.Localize("Message")); ImGui.NextColumn();
            ImGui.Text(_localizer.Localize("Time")); ImGui.NextColumn();
            if (Config.Coord)
            {
                ImGui.Text(_localizer.Localize("Retrieve")); ImGui.NextColumn();
            }
            if (Config.Teleport)
            {
                ImGui.Text(_localizer.Localize("Teleport")); ImGui.NextColumn();
            }
            ImGui.Text(_localizer.Localize("Delete")); ImGui.NextColumn();
            ImGui.Separator();
            int delete = -1;
            for (int i = 0; i < Config.MapLinkMessageList.Count(); i++)
            {
                var maplinkMessage = Config.MapLinkMessageList[i];
                ImGui.Text(maplinkMessage.Sender); ImGui.NextColumn();
                ImGui.TextWrapped(maplinkMessage.Text); ImGui.NextColumn();
                ImGui.Text(maplinkMessage.RecordTime.ToString()); ImGui.NextColumn();
                if (Config.Coord)
                {
                    if(ImGui.Button(_localizer.Localize("View") + "##" + i.ToString() ))
                    {
                        Plugin.Log($"Viewing {maplinkMessage.Text}");
                        Plugin.CommandManager.ProcessCommand($"/coord {maplinkMessage.X} {maplinkMessage.Y}: {maplinkMessage.PlaceName}");
                    }
                    ImGui.NextColumn();
                }
                if (Config.Teleport)
                {
                    if (ImGui.Button(_localizer.Localize("Tele") + "##" + i.ToString()))
                    {
                        var aetheryteName = Plugin.GetNearestAetheryte(maplinkMessage);
                        if(aetheryteName != "")
                        {
                            Plugin.Log($"Teleporting to {aetheryteName}");
                            Plugin.CommandManager.ProcessCommand($"/tp {aetheryteName}");
                        }
                        else
                        {
                            Plugin.LogError($"Cannot find nearest aetheryte of {maplinkMessage.PlaceName}({maplinkMessage.X}, {maplinkMessage.Y}).");
                        }
                    }
                    ImGui.NextColumn();
                }
                if (ImGui.Button(_localizer.Localize("Del") + "##" + i.ToString()))
                {
                    delete = i;
                }
                ImGui.NextColumn();
                ImGui.Separator();
            }
            if (delete != -1)
            {
                Config.MapLinkMessageList.RemoveAt(delete);
                Config.Save();
            }

        }

    }
}