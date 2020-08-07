using System;
using Dalamud;
using System.Collections.Generic;
using Lumina.Excel.GeneratedSheets;
using Dalamud.DiscordBot;

namespace MapLinker.Objects
{
    public class MapLinkMessage
    {
        public static MapLinkMessage Empty => new MapLinkMessage(0, string.Empty, string.Empty, 0, 0, 100, 0, string.Empty, DateTime.Now);

        public ushort ChatType;
        public string Sender;
        public string Text;
        public float X;
        public float Y;
        public float Scale;
        public uint TerritoryId;
        public string PlaceName;
        public DateTime RecordTime;

        public MapLinkMessage(ushort chatType, string sender, string text, float x, float y, float scale, uint territoryId, string placeName, DateTime recordTime)
        {
            ChatType = chatType;
            Sender = sender;
            Text = text;
            X = x;
            Y = y;
            Scale = scale;
            TerritoryId = territoryId;
            PlaceName = placeName;
            RecordTime = recordTime;
        }

    }
}
