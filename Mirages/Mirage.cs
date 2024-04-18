using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using OTAPI;
using Terraria;
using Terraria.ID;
using Terraria.Net.Sockets;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace Mirages
{
    public class Mirage : IEnumerable<MirageTile>
    {
        MirageTile[,] Tiles;
        Rectangle Area;

        public int X => Area.X;
        public int Y => Area.Y;
        public short Width => (short)Area.Width;
        public short Height => (short)Area.Height;

        public MirageTile this[int x, int y]
        {
            get => Tiles[x - X, y - Y];
        }

        public Mirage(int x, int y, int width, int height) : this(new Rectangle(x, y, width, height))
        {
        }
        public Mirage(Rectangle area) 
        {
            Area = area;
            Tiles = new MirageTile[Width, Height];

            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                {
                    Tiles[i, j] = new MirageTile(i + X, j + Y);
                }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<MirageTile> GetEnumerator()
        {
            var list = new List<MirageTile>();
            for (int j = 0; j < Height; j++)
                for (int i = 0; i < Width; i++)
                {
                    list.Add(Tiles[i, j]);
                }
            return list.GetEnumerator();
        }

        public void SendAll(bool tileFrame, Func<TSPlayer, bool> predicate = null)
        {
            Send(tileFrame, predicate == null ? 
                TShock.Players : 
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void Send(bool tileFrame, params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            Send(players, GetPacket10Data());

            if (!tileFrame)
            {
                return;
            }
            foreach (var i in players)
            {
                if (i == null || !i.ConnectionAlive)
                    continue;

                i.SendData(
                    PacketTypes.TileFrameSection, 
                    null, 
                    Netplay.GetSectionX(X), 
                    Netplay.GetSectionY(Y), 
                    Netplay.GetSectionX(X + Width), 
                    Netplay.GetSectionY(Y + Height));
            }
        }

        public void SendAll(TileChangeType type, Func<TSPlayer, bool> predicate = null)
        {
            Send(type, predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void Send(TileChangeType type, params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            foreach (var i in GetPacket20Data(type))
            {
                Send(players, i);
            }
        }

        void Send(TSPlayer[] players, byte[] data)
        {
            foreach (var i in players)
            {
                if (i == null || !i.ConnectionAlive)
                    continue;

                var socket = Netplay.Clients[i.Index].Socket;
                var callback = (SocketSendCallback)Netplay.Clients[i.Index].ServerWriteCallBack;

                Hooks.NetMessage.InvokeSendBytes(
                    socket,
                    data,
                    0,
                    data.Length,
                    callback,
                    null,
                    i.Index);
            }
        }

        byte[] GetPacket10Data()
        {
            byte[] data;
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.Write(this);
                }
                data = s.ToArray();
            }
            using (var s = new MemoryStream())
            {
                using (var w = new DeflateStream(s, CompressionMode.Compress, true))
                {
                    w.Write(data, 0, data.Length);
                }
                data = s.ToArray();
            }
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)10);
                    w.Write(data);
                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
        }

        List<byte[]> GetPacket20Data(TileChangeType type)
        {
            var data = new List<byte[]>();
            var countX = (int)Math.Ceiling(Width / 255f);
            var countY = (int)Math.Ceiling(Height / 255f);

            for (int i = 0; i < countX; i++)
            {
                for (int j = 0; j < countY; j++)
                {
                    var x = X + (i * 255);
                    var y = Y + (j * 255);
                    var width = Math.Min(255, Width - (i * 255));
                    var height = Math.Min(255, Height - (j * 255));
                    data.Add(GetPacket20Data(x, y, width, height, type));
                }
            }
            return data;
        }

        byte[] GetPacket20Data(int x, int y, int width, int height, TileChangeType type)
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s)) // учесть то, что длина-ширина не должна быть больше 255
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)20);
                    w.Write(this, x, y, width, height, type);
                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
        }
    }

    public class MirageTile : Tile
    {
        public int X;
        public int Y;

        public MirageTile(int x, int y) : base(Main.tile[x, y] ?? new Tile())
        {
            X = x;
            Y = y;
        }
    }
}
