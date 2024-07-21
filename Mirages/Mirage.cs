using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.ID;
using TShockAPI;
using Microsoft.Xna.Framework;

//переопределить некоторые методы из Tile для MirageTile по типу ClearTile и т.д. (для сундуков и табличек)

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
        public List<MirageTile> Signs => GetList(t => t.SignID > -1);
        public List<MirageTile> Chests => GetList(t => t.ChestID > -1);

        public MirageTile this[int x, int y]
        {
            get => Tiles[x - X, y - Y];
        }

        public Mirage(int sectionX, int sectionY) : 
            this(new Rectangle(
                Math.Max(0, Math.Min(sectionX, Main.maxSectionsX - 1)) * 200,
                Math.Max(0, Math.Min(sectionY, Main.maxSectionsY - 1)) * 150, 
                200, 
                150))
        {
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
        public IEnumerator<MirageTile> GetEnumerator() => GetList().GetEnumerator();

        public List<MirageTile> GetList(Func<MirageTile, bool> predicate = null)
        {
            var list = new List<MirageTile>();
            for (int j = 0; j < Height; j++)
                for (int i = 0; i < Width; i++)
                {
                    if (predicate == null || predicate(Tiles[i, j]))
                        list.Add(Tiles[i, j]);
                }
            return list;
        }

        public Tuple<SetObjectResult, Point> SetSign(int left, int top, int signID, string text, SignType type = SignType.Sign, bool ignoreAnotherSign = false, byte color = 0, bool fullbright = false, bool invisible = false, bool inActive = false, params Tile2x2Point[] points)
        {
            return Set2x2Object(
               left,
               top,
               color,
               fullbright,
               invisible,
               inActive,
               points,
               (i)    => i.SetSignText(signID, text, type, ignoreAnotherSign),
               (i, j) => i.SetSignTile(type, j, ignoreAnotherSign));
        }

        public Tuple<SetObjectResult, Point> SetChest(int left, int top, int chestID, string name, ChestType type = ChestType.Chest, IEnumerable<SlotItem> content = null, bool ignoreAnotherChest = false, byte color = 0, bool fullbright = false, bool invisible = false, bool inActive = false, params Tile2x2Point[] points)
        {
            return Set2x2Object(
                left, 
                top, 
                color, 
                fullbright, 
                invisible, 
                inActive, 
                points, 
                (i)    => i.SetChest(chestID, name, type, ignoreAnotherChest, content.ToArray()),
                (i, j) => i.SetChestTile(type, j, ignoreAnotherChest));
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
            players.SendPacket(GetPacket10Data());

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
                players.SendPacket(i);
            }
        }

        public void SendOriginalAll(Func<TSPlayer, bool> predicate = null)
        {
            SendOriginal(predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void SendOriginal(params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            foreach (var i in players.Where(i => i != null && i.ConnectionAlive))
            {
                i.SendData(PacketTypes.TileSendSection, null, 
                    X, 
                    Y, 
                    Width + 1, 
                    Height + 1);

                i.SendData(PacketTypes.TileFrameSection, null, 
                    Netplay.GetSectionX(X), 
                    Netplay.GetSectionY(Y), 
                    Netplay.GetSectionX(X + Width + 1), 
                    Netplay.GetSectionY(Y + Height + 1));
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

        Tuple<SetObjectResult, Point> Set2x2Object(int left, int top, byte color, bool fullbright, bool invisible, bool inActive, Tile2x2Point[] points, Func<MirageTile, SetObjectResult> funcOne, Func<MirageTile, Tile2x2Point, SetObjectResult> funcTwo)
        {
            if (!Area.Contains(left, top))
            {
                return Tuple.Create(SetObjectResult.ObjectOutsideMirageArea, new Point(left, top));
            }
            var result = funcOne(this[left, top]);

            if (result != SetObjectResult.Success)
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
                    return Tuple.Create(SetObjectResult.ObjectOutsideMirageArea, new Point(x, y));
                }
                result = funcTwo(this[x, y], p);

                if (result != SetObjectResult.Success)
                {
                    return Tuple.Create(result, new Point(x, y));
                }
                this[x, y].inActive(inActive);
                this[x, y].color(color);
                this[x, y].invisibleBlock(invisible);
                this[x, y].fullbrightBlock(fullbright);
            }
            return Tuple.Create(SetObjectResult.Success, new Point(left, top));
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
        public static readonly ReadOnlyDictionary<ChestType, int> ChestTypes = new ReadOnlyDictionary<ChestType, int>(new Dictionary<ChestType, int>
        {
            { ChestType.Chest, 21 },
            { ChestType.GoldChest, 21 },
            { ChestType.ShadowChest, 21 },
            { ChestType.Barrel, 21 },
            { ChestType.Trash, 21 },
        });

        public int X { get; private set; }
        public int Y { get; private set; }

        public int SignID
        {
            get => signID;
        }
        public string SignText
        {
            get => signText ?? "";
            set => signText = value;
        }
        int signID = -1;
        string signText = "";

        public int ChestID
        {
            get => chestID;
        }
        public string ChestName
        {
            get => chestName ?? "";
            set => chestName = value;
        }
        int chestID = -1;
        string chestName = "";
        public List<SlotItem> ChestContent = new List<SlotItem>();

        public override void ClearEverything()
        {
            signID = chestID = -1;
            base.ClearEverything();
        }

        public override void ClearTile()
        {
            signID = chestID = -1;
            base.ClearTile();
        }

        public MirageTile(int x, int y) : base(Main.tile[x, y] ?? new Tile())
        {
            X = x;
            Y = y;

            if (SignTypes.Values.Contains(type) && frameX % 36 == 0 && frameY % 36 == 0)
            {
                signID = Sign.ReadSign(x, y);
                if (signID > -1)
                {
                    signText = Main.sign[signID].text;
                }
            }

            if ((TileID.Sets.BasicChest[type] || type == 88) && frameX % (type == 88 ? 54 : 36) == 0 && frameY % 36 == 0)
            {
                chestID = Chest.FindChest(x, y);
                if (chestID > -1)
                {
                    var chest = Main.chest[chestID];
                    for (int i = 0; i < chest.item.Length; i++)
                    {
                        var j = chest.item[i];
                        if (j == null || !j.active || j.netID <= 0 || j.stack <= 0)
                        {
                            continue;
                        }
                        ChestContent.Add(new SlotItem(i, j.netID, j.stack, j.prefix));
                    }
                    chestName = chest.name;
                }
            }
        }

        public SetObjectResult SetSignText(int signID, string text, SignType type = SignType.Sign, bool ignoreAnotherSign = false)
        {
            if (!ignoreAnotherSign && this.signID > -1 && !string.IsNullOrWhiteSpace(signText))
            {
                return SetObjectResult.OccupiedByAnotherObject;
            }
            SetSignTile(type, Tile2x2Point.LeftTop, ignoreAnotherSign);

            this.signID = signID;
            signText = text;

            return SetObjectResult.Success;
        }

        public SetObjectResult SetSignTile(SignType type = SignType.Sign, Tile2x2Point point = Tile2x2Point.LeftTop, bool ignoreAnotherSign = false)
        {
            if (!ignoreAnotherSign && signID > -1 && !string.IsNullOrWhiteSpace(signText) && point != Tile2x2Point.LeftTop)
            {
                return SetObjectResult.OccupiedByAnotherObject;
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
            return SetObjectResult.Success;
        }

        public SetObjectResult SetChest(int chestID, string name, ChestType type = ChestType.Chest, bool ignoreAnotherChest = false, params SlotItem[] content)
        {
            if (!ignoreAnotherChest && this.chestID > -1 && (ChestContent.Count > 0 || !string.IsNullOrWhiteSpace(chestName)))
            {
                return SetObjectResult.OccupiedByAnotherObject;
            }
            SetChestTile(type, Tile2x2Point.LeftTop, ignoreAnotherChest);

            if (content != null && content.Length > 0)
            {
                ChestContent.AddRange(content);
            }
            this.chestID = chestID;
            chestName = name;

            return SetObjectResult.Success;
        }

        public SetObjectResult SetChestTile(ChestType type = ChestType.Chest, Tile2x2Point point = Tile2x2Point.LeftTop, bool ignoreAnotherChest = false)
        {
            if (!ignoreAnotherChest && chestID > -1 && (ChestContent.Count > 0 || !string.IsNullOrWhiteSpace(chestName)) && point != Tile2x2Point.LeftTop)
            {
                return SetObjectResult.OccupiedByAnotherObject;
            }
            int typeN = (int)type;

            sTileHeader = 32;
            bTileHeader = bTileHeader2 = bTileHeader3 = 0;
            this.type = (ushort)ChestTypes[type];

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
            frameX += (short)(typeN * 36);

            return SetObjectResult.Success;
        }

        public void SendSignAll(bool TBD, Func<TSPlayer, bool> predicate = null)
        {
            SendSign(TBD, predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void SendSign(bool TBD, params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            var data = GetPacket47Data(TBD);

            foreach (var i in players)
            {
                if (i == null)
                { 
                    continue; 
                }
                data[data.Length - 2] = (byte)i.Index;
                i.SendPacket(data);
            }
        }

        public void SendChestContentAll(Func<TSPlayer, bool> predicate = null)
        {
            SendChestContent(predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void SendChestContent(params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            for (int i = 0; i < 40; i++)
            {
                var index = ChestContent.FindIndex(j => j.SlotID == i);
                SendChestItem(index > -1 ? ChestContent[index] : new SlotItem(i, 0), players);
            }
            players.SendPacket(GetPacket33Data());
        }

        public void SendChestItemAll(byte slotID, Func<TSPlayer, bool> predicate = null)
        {
            SendChestItem(slotID, predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void SendChestItem(byte slotID, params TSPlayer[] players)
        {
            var index = ChestContent.FindIndex(i => i.SlotID == slotID);
            if (index == -1)
            {
                return;
            }
            SendChestItem(ChestContent[index], players);
        }

        public void SendChestItem(SlotItem item, params TSPlayer[] players)
        {
            if (item.SlotID < 0 || item.SlotID >= 40)
            {
                return;
            }
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            players.SendPacket(GetPacket32Data(item));
        }

        public void SendChestNameAll(Func<TSPlayer, bool> predicate = null)
        {
            SendChestName(predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void SendChestName(params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            players.SendPacket(GetPacket69Data());
        }

        public void SendChestSyncIndexAll(Func<TSPlayer, bool> predicate = null)
        {
            SendChestSyncIndex(predicate == null ?
                TShock.Players :
                TShock.Players.Where(i => predicate(i)).ToArray());
        }

        public void SendChestSyncIndex(params TSPlayer[] players)
        {
            if (players == null || players.Length == 0 || players.All(i => i == null || !i.ConnectionAlive))
            {
                return;
            }
            var data = GetPacket80Data();

            foreach (var i in players)
            {
                if (i == null)
                {
                    continue;
                }
                data[data.Length - 3] = (byte)i.Index;
                i.SendPacket(data);
            }
        }

        byte[] GetPacket47Data(bool TBD)
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)47);

                    w.Write((short)signID);
                    w.Write((short)X);
                    w.Write((short)Y);
                    w.Write(SignText);
                    w.Write((byte)0);
                    w.Write(TBD);

                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
        }

        byte[] GetPacket32Data(SlotItem item)
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)32);

                    w.Write((short)chestID);
                    w.Write((byte)item.SlotID);
                    w.Write((short)item.Stack);
                    w.Write((byte)item.Prefix);
                    w.Write((short)item.NetID);

                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
        }

        byte[] GetPacket33Data()
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)33);

                    w.Write((short)chestID);
                    w.Write((short)X);
                    w.Write((short)Y);
                    w.Write((byte)ChestName.Length);

                    if (ChestName.Length > 0 && ChestName.Length <= 20)
                    {
                        w.Write(ChestName);
                    }
                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
        }

        byte[] GetPacket69Data()
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)69);

                    w.Write((short)chestID);
                    w.Write((short)X);
                    w.Write((short)Y);
                    w.Write(ChestName);
                    w.Write((byte)0);

                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
        }

        byte[] GetPacket80Data()
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)80);

                    w.Write((byte)0);
                    w.Write((short)chestID);

                    var length = (ushort)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(length);
                }
                return s.ToArray();
            }
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

    public enum SetObjectResult : byte
    {
        Success = 0, 
        InvalidTileID = 1, 
        OccupiedByAnotherObject = 2,
        ObjectOutsideMirageArea = 3,
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

    public enum ChestType : int
    {
        Chest = 0,
        GoldChest = 1,
        ShadowChest = 3,
        Barrel = 5,
        Trash = 6,
    }

    public enum Tile2x2Point : byte
    {
        LeftTop = 0,
        LeftBottom = 1,
        RightTop = 2,
        RightBottom = 3,
    }
}
