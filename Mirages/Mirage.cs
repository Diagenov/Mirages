using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public Tuple<SetSignResult, Point> SetSign(int left, int top, int signID, string text, SignType type = SignType.Sign, bool ignoreAnotherSigns = false, byte color = 0, bool fullbright = false, bool invisible = false, bool inActive = false, params Tile2x2Point[] points)
        {
            if (!Area.Contains(left, top))
            {
                return Tuple.Create(SetSignResult.SignOutsideMirageArea, new Point(left, top));
            }
            var result = this[left, top].SetSignText(signID, text, type, ignoreAnotherSigns);

            if (result != SetSignResult.Success)
            {
                return Tuple.Create(result, new Point(left, top));
            }
            if (points == null || points.Length == 0)
            {
                points = Enum.GetValues<Tile2x2Point>();
            }
            var list = new List<Tile2x2Point>(points);

            if (!list.Contains(Tile2x2Point.LeftTop))
            {
                list.Add(Tile2x2Point.LeftTop);
            }
            foreach (var p in list)
            {
                int x = left;
                int y = top;
                Set2x2Delta(ref x, ref y, p);

                if (!Area.Contains(x, y))
                {
                    return Tuple.Create(SetSignResult.SignOutsideMirageArea, new Point(x, y));
                }
                result = this[x, y].SetSignTile(type, p, ignoreAnotherSigns);

                if (result != SetSignResult.Success)
                {
                    return Tuple.Create(result, new Point(x, y));
                }
                this[x, y].inActive(inActive);
                this[x, y].color(color);
                this[x, y].invisibleBlock(invisible);
                this[x, y].fullbrightBlock(fullbright);
            }
            return Tuple.Create(SetSignResult.Success, new Point(left, top));
        }

        //аналогично для сундуков

        //также для сундуков стоит добавить фантомное взаимодействие...

        //добавить ли фантомное взаимодействие табличкам???

        //добавить метод получения оригинала (то, что в действительности на месте фантомной области)

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
    
        void Set2x2Delta(ref int X, ref int Y, Tile2x2Point refPoint)
        {
            switch (refPoint)
            {
                case Tile2x2Point.RightTop:
                    X++;
                    break;

                case Tile2x2Point.LeftBottom:
                    Y++;
                    break;

                case Tile2x2Point.RightBottom:
                    X++;
                    Y++;
                    break;
            }
        }
    }

    public class MirageTile : Tile
    {
        public static readonly ReadOnlyDictionary<SignType, int> SignTypes = new ReadOnlyDictionary<SignType, int>(new Dictionary<SignType, int>
        {
            { SignType.Sign, 55 },
            { SignType.AnnouncementBox, 425 },
            { SignType.TatteredWoodSign, 573 },
            { SignType.Tombstone, 85 },
            { SignType.GraveMarker, 85 },
            { SignType.CrossGraveMarker, 85 },
            { SignType.Headstone, 85 },
            { SignType.Gravestone, 85 },
            { SignType.Obelisk, 85 },
            { SignType.GoldenCrossGraveMarker, 85 },
            { SignType.GoldenTombstone, 85 },
            { SignType.GoldenGraveMarker, 85 },
            { SignType.GoldenGravestone, 85 },
            { SignType.GoldenHeadstone, 85 },
        });

        public int X;
        public int Y;

        public int SignID = -1;
        public string SignText = "";

        public int ChestID = -1;
        public string ChestName = "";
        public List<SlotItem> ChestItems = new List<SlotItem>();

        public MirageTile(int x, int y) : base(Main.tile[x, y] ?? new Tile())
        {
            X = x;
            Y = y;

            if (SignTypes.Values.Contains(type) && frameX % 36 == 0 && frameY % 36 == 0)
            {
                SignID = Sign.ReadSign(x, y);
                if (SignID > -1)
                {
                    SignText = Main.sign[SignID].text;
                }
            }

            if ((TileID.Sets.BasicChest[type] || type == 88) && frameX % (type == 88 ? 54 : 36) == 0 && frameY % 36 == 0)
            {
                ChestID = Chest.FindChest(x, y);
                if (ChestID > -1)
                {
                    var chest = Main.chest[ChestID];
                    for (int i = 0; i < chest.item.Length; i++)
                    {
                        var j = chest.item[i];
                        if (j == null || !j.active || j.netID <= 0 || j.stack <= 0)
                        {
                            continue;
                        }
                        ChestItems.Add(new SlotItem(i, j.netID, j.stack, j.prefix));
                    }
                    ChestName = chest.name;
                }
            }
        }

        public SetSignResult SetSignText(int signID, string text, SignType type = SignType.Sign, bool ignoreAnotherSigns = false)
        {
            if (!ignoreAnotherSigns && SignID > -1 && !string.IsNullOrWhiteSpace(SignText))
            {
                return SetSignResult.OccupiedByAnotherSign;
            }
            SetSignTile(type, Tile2x2Point.LeftTop, ignoreAnotherSigns);

            SignID = signID;
            SignText = text;

            return SetSignResult.Success;
        }

        public SetSignResult SetSignTile(SignType type = SignType.Sign, Tile2x2Point point = Tile2x2Point.LeftTop, bool ignoreAnotherSigns = false)
        {
            if (!ignoreAnotherSigns && SignID > -1 && !string.IsNullOrWhiteSpace(SignText) && point != Tile2x2Point.LeftTop)
            {
                return SetSignResult.OccupiedByAnotherSign;
            }
            int typeN = (int)type;

            sTileHeader = 32;
            bTileHeader = bTileHeader2 = bTileHeader3 = 0;
            this.type = (ushort)SignTypes[type];

            frameX = 0;
            frameY = 0;

            if (point == Tile2x2Point.LeftBottom || point == Tile2x2Point.RightBottom)
            {
                frameY += 18;
            }
            if (point == Tile2x2Point.RightTop || point == Tile2x2Point.RightBottom)
            {
                frameX += 18;
            }
            if (typeN < 0)
            {
                frameX += 144;
            }
            else
            {
                frameX += (short)(typeN * 36);
            }
            return SetSignResult.Success;
        }
    }

    public struct SlotItem
    {
        public int SlotID;
        public int NetID;
        public int Stack;
        public int Prefix;

        public SlotItem(int slotID, int netID, int stack = 1, int prefix = 0)
        {
            SlotID = slotID;
            NetID = netID;
            Stack = stack;
            Prefix = prefix;
        }
    }

    public enum SetSignResult : byte
    {
        Success = 0, 
        InvalidTileID = 1, 
        OccupiedByAnotherSign = 2,
        SignOutsideMirageArea = 3,
    }

    public enum SignType : int
    {
        Sign = -3,
        AnnouncementBox = -2,
        TatteredWoodSign = -1,
        Tombstone = 0,
        GraveMarker = 1,
        CrossGraveMarker = 2,
        Headstone = 3,
        Gravestone = 4,
        Obelisk = 5,
        GoldenCrossGraveMarker = 6,
        GoldenTombstone = 7,
        GoldenGraveMarker = 8,
        GoldenGravestone = 9,
        GoldenHeadstone = 10,
    }

    public enum Tile2x2Point : byte
    {
        LeftTop = 0,
        LeftBottom = 1,
        RightTop = 2,
        RightBottom = 3,
    }
}
