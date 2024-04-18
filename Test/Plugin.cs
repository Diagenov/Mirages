using System;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using Mirages;

namespace WireCensor
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
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
            if (e.MsgID != PacketTypes.MassWireOperation)
            {
                return;
            }
            using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
            {
                int startX = reader.ReadInt16();
                int startY = reader.ReadInt16();
                int endX = reader.ReadInt16();
                int endY = reader.ReadInt16();
                var mode = reader.ReadByte();

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

                var mirage = new Mirage(area);
                foreach (var i in mirage)
                {
                    if ((mode & 1) == 1)
                    {
                        i.wire(true);
                    }
                    if ((mode & 2) == 2)
                    {
                        i.wire2(true);
                    }
                    if ((mode & 4) == 4)
                    {
                        i.wire3(true);
                    }
                    if ((mode & 8) == 8)
                    {
                        i.wire4(true);
                    }
                    if ((mode & 16) == 16)
                    {
                        i.actuator(true);
                    }
                }
                mirage.SendAll(true);

                foreach (var i in mirage)
                {
                    i.active(true);
                    i.type = 0;
                }
                mirage.SendAll(Terraria.ID.TileChangeType.None);
            }
        }
    }
}