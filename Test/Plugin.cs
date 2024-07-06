using System;
using System.IO;
using System.Collections.Generic;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using Mirages;
using System.Linq;
using Terraria.ID;

namespace WireCensor
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        static List<Mirage> mirages = new List<Mirage>();

        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            }
            base.Dispose(disposing);
        }

        void OnGetData(GetDataEventArgs e)
        {
            var player = TShock.Players[e.Msg.whoAmI];
            if (player == null)
            {
                return;
            }
            if (e.MsgID == PacketTypes.SignRead || e.MsgID == PacketTypes.ChestGetContents)
            {
                var X = 0;
                var Y = 0;
                using (var s = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                {
                    using (var r = new BinaryReader(s))
                    {
                        var point = r.ReadPoint16();
                        X = point.X;
                        Y = point.Y;
                    }
                }
                var find = mirages.Find(i => (e.MsgID == PacketTypes.SignRead ? i.Signs : i.Chests).Any(j => j.X == X && j.Y == Y));
                if (find == null)
                {
                    return;
                }
                var tile = find.Chests.Find(j => j.X == X && j.Y == Y);

                if (e.MsgID == PacketTypes.SignRead)
                {
                    tile.SendSign(false, player);
                }
                else
                {
                    tile.SendChestContent(player);
                }
                e.Handled = true;
                return;
            }
            if (e.MsgID != PacketTypes.MassWireOperation)
            {
                return;
            }
            using (var r = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
            {
                int startX = r.ReadInt16();
                int startY = r.ReadInt16();
                int endX = r.ReadInt16();
                int endY = r.ReadInt16();
                var mode = r.ReadByte();

                if ((mode & (1 | 2 | 4 | 8 | 16)) == 0)
                {
                    return;
                }
                if (!WorldGen.InWorld(startX, startY) || !WorldGen.InWorld(endX, endY))
                {
                    return;
                }

                var area = new Rectangle(
                    Math.Min(startX, endX),
                    Math.Min(startY, endY),
                    Math.Abs(endX - startX) + 1,
                    Math.Abs(endY - startY) + 1);

                var signs = new List<Tile2x2Point>();
                var signInActive = false;

                var chests = new List<Tile2x2Point>();
                var content = new List<SlotItem>();
                var chestInActive = false;

                var mirage = new Mirage(area);
                mirages.Add(mirage);

                foreach (var i in mirage)
                {
                    if ((mode & 1) == 1)
                    {
                        i.wire(true);
                        signs.Add(Tile2x2Point.LeftTop);
                        chests.Add(Tile2x2Point.LeftTop);
                        content.Add(new SlotItem(0, ItemID.Bass, 59));
                    }
                    if ((mode & 2) == 2)
                    {
                        i.wire2(true);
                        signs.Add(Tile2x2Point.LeftBottom);
                        chests.Add(Tile2x2Point.LeftBottom);
                        content.Add(new SlotItem(11, ItemID.TrashCan, 3));
                    }
                    if ((mode & 4) == 4)
                    {
                        i.wire3(true);
                        signs.Add(Tile2x2Point.RightTop);
                        chests.Add(Tile2x2Point.RightTop);
                        content.Add(new SlotItem(25, ItemID.Barrel, 1));
                    }
                    if ((mode & 8) == 8)
                    {
                        i.wire4(true);
                        signs.Add(Tile2x2Point.RightBottom);
                        chests.Add(Tile2x2Point.RightBottom);
                        content.Add(new SlotItem(31, ItemID.WireKite, 999));
                    }
                    if ((mode & 16) == 16)
                    {
                        i.actuator(true);
                        signInActive = true;
                        chestInActive = true;
                        content.Add(new SlotItem(38, ItemID.Actuator, 146));
                    }
                }
                mirage.SendAll(true);

                foreach (var i in mirage)
                {
                    i.active(true);
                    i.ResetToType(0);
                }
                mirage.SendAll(TileChangeType.None);

                if (mirage.Height > 2 && mirage.Width > 2)
                {
                    var result = mirage.SetSign(
                        mirage.X + 1, 
                        mirage.Y + 1, 
                        900, 
                        ":D", 
                        SignType.Sign, 
                        true, 
                        26,
                        false,
                        false,
                        signInActive,
                        signs.ToArray());
                    
                    player.SendInfoMessage($"SignResult: [c/ffffff:{result.Item1}], X: [c/ffffff:{result.Item2.X}], Y: [c/ffffff:{result.Item2.Y}]");
                }
                if (mirage.Height > 5 && mirage.Width > 5)
                {
                    var result = mirage.SetChest(
                        mirage.X + 3,
                        mirage.Y + 3,
                        1000,
                        "D:",
                        ChestType.Barrel,
                        content,
                        true,
                        13,
                        false,
                        false,
                        chestInActive,
                        signs.ToArray());

                    player.SendInfoMessage($"ChestResult: [c/ffffff:{result.Item1}], X: [c/ffffff:{result.Item2.X}], Y: [c/ffffff:{result.Item2.Y}]");
                }
                mirage.SendAll(true);
            }
        }
    }
}