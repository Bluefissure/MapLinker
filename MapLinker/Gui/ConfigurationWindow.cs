using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using MapLinker.Objects;

namespace MapLinker.Gui
{
    public class ConfigurationWindow : Window<MapLinker>
    {

        public Configuration Config => Plugin.Config;

        public ConfigurationWindow(MapLinker plugin) : base(plugin)
        {
        }

        protected override void DrawUi()
        {
            ImGui.SetNextWindowSize(new Vector2(530, 450), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin($"{Plugin.Name} Panel", ref WindowVisible, ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.End();
                return;
            }

            if (ImGui.BeginChild("##SettingsRegion"))
            {
                if (ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen))
                    DrawGeneralSettings();
                if (ImGui.CollapsingHeader("Records"))
                    DrawMaplinks();

                ImGui.EndChild();
            }

            ImGui.End();
        }

        

        private void DrawGeneralSettings()
        {
            if (ImGui.Checkbox("Recording", ref Config.Recording)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Automatically record messages with maplink and retrieve later.");
            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextUnformatted("Tooltips");
            ImGui.AlignTextToFramePadding();
            ImGui.SameLine();
            if (ImGui.Checkbox("##hideTooltipsOnOff", ref Config.ShowTooltips)) Config.Save();


            if (ImGui.Checkbox("Call /coord to retrieval map links", ref Config.Coord)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Add an option to call /coord  to retrieval maplinks.\n" +
                                 "Make sure you have downloaded ChatCoordinates Plugin.");

            if (ImGui.Checkbox("Call /tp to teleport to the nearest aetheryte", ref Config.Teleport)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Add an option to call /tp to teleport to the nearest aetheryte.\n" +
                                 "Make sure you have downloaded Teleporter Plugin.");
            if (ImGui.Checkbox("Print Debug Message", ref Config.PrintMessage)) Config.Save();
            if (ImGui.Checkbox("Print Error Message", ref Config.PrintError)) Config.Save();

        }
        private void DrawMaplinks()
        {
            // sender, text, time, view, tp, del
            int columns = 4;
            if (Config.Coord) columns++;
            if (Config.Teleport) columns++;
            ImGui.Columns(columns, "Maplinks", true);
            ImGui.Separator();
            ImGui.Text("Sender"); ImGui.NextColumn();
            ImGui.Text("Message"); ImGui.NextColumn();
            ImGui.Text("Time"); ImGui.NextColumn();
            if (Config.Coord)
            {
                ImGui.Text("Retrieve"); ImGui.NextColumn();
            }
            if (Config.Teleport)
            {
                ImGui.Text("Teleport"); ImGui.NextColumn();
            }
            ImGui.Text("Delete"); ImGui.NextColumn();
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
                    if(ImGui.Button("View##" + i.ToString()))
                    {
                        Plugin.Log($"Viewing {maplinkMessage.Text}");
                        Plugin.CommandManager.ProcessCommand($"/coord {maplinkMessage.X} {maplinkMessage.Y}: {maplinkMessage.PlaceName}");
                    }
                    ImGui.NextColumn();
                }
                if (Config.Teleport)
                {
                    if (ImGui.Button("Tele##" + i.ToString()))
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
                if (ImGui.Button("Del##" + i.ToString()))
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
            ImGui.Columns(1);
            if (ImGui.Button("Clear"))
            {
                Config.MapLinkMessageList.Clear();
                Config.Save();
            }

        }

      
    }
}