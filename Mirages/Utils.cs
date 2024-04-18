using System;
using System.IO;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace Mirages
{
    internal static class Utils
    {
        internal static void Write(this BinaryWriter w, Mirage mirage, int x, int y, int width, int height, TileChangeType type)
        {
            w.Write((short)x);
            w.Write((short)y);
            w.Write((byte)width);
            w.Write((byte)height);
            w.Write((byte)type);

            for (int i = x; i < x + width; i++)
            {
                for (int j = y; j < y + height; j++)
                {
                    w.Write(mirage[i, j]);
                }
            }
        }

        static void Write(this BinaryWriter w, MirageTile tile)
        {
            w.Write(new BitsByte(
                tile.active(),
                false,
                tile.wall > 0,
                tile.liquid > 0,
                tile.wire(),
                tile.halfBrick(),
                tile.actuator(),
                tile.inActive()));

            w.Write(new BitsByte(
                tile.wire2(),
                tile.wire3(),
                tile.active() && tile.color() > 0,
                tile.wall > 0 && tile.wallColor() > 0,
                tile.slope() == 1 || tile.slope() == 3,
                tile.slope() == 2 || tile.slope() == 3,
                tile.slope() == 4,
                tile.wire4()));

            w.Write(new BitsByte(
                tile.fullbrightBlock(),
                tile.fullbrightWall(),
                tile.invisibleBlock(),
                tile.invisibleWall()));

            if (tile.active() && tile.color() > 0)
            {
                w.Write(tile.color());
            }
            if (tile.wall > 0 && tile.wallColor() > 0)
            {
                w.Write(tile.wallColor());
            }
            if (tile.active())
            {
                w.Write(tile.type);
                if (Main.tileFrameImportant[tile.type])
                {
                    w.Write(tile.frameX);
                    w.Write(tile.frameY);
                }
            }
            if (tile.wall > 0)
            {
                w.Write(tile.wall);
            }
            if (tile.liquid > 0)
            {
                w.Write(tile.liquid);
                w.Write(tile.liquidType());
            }
        }

        internal static void Write(this BinaryWriter w, Mirage mirage)
        {
            var last = default(ITile);
            var same = default(short);
            var data = new List<byte>();
            var flag = Flag1.None;

            w.Write(mirage.X);
            w.Write(mirage.Y);
            w.Write(mirage.Width);
            w.Write(mirage.Height);

            foreach (var i in mirage)
            {
                if (i.isTheSameAs(last) && TileID.Sets.AllowsSaveCompressionBatching[i.type])
                {
                    same++;
                    continue;
                }
                if (last != null)
                {
                    w.Write(data, ref same, ref flag);
                }
                last = WriteTile(i, ref flag, data);
            }
            w.Write(data, ref same, ref flag);
            w.Write((short)0); // chests count
            w.Write((short)0); // signs count
            w.Write((short)0); // entities count 
        }

        static void Write(this BinaryWriter w, List<byte> data, ref short same, ref Flag1 flag1)
        {
            if (same > 0)
            {
                var bytes = BitConverter.GetBytes(same);
                if (bytes[1] > 0)
                {
                    data.AddRange(bytes);
                    flag1 |= Flag1.TwoBytesSame;
                }
                else
                {
                    data.Add(bytes[0]);
                    flag1 |= Flag1.OneByteSame;
                }
            }
            data.Insert(0, (byte)flag1);
            w.Write(data.ToArray(), 0, data.Count);
            flag1 = Flag1.None;
            same = 0;
        }

        static ITile WriteTile(ITile tile, ref Flag1 flag1, List<byte> data)
        {
            var flag2 = Flag2.None;
            var flag3 = Flag3.None;
            var flag4 = Flag4.None;

            data.Clear();
            WriteBlock(data, tile, ref flag1, ref flag3);
            WriteWall(data, tile, ref flag1, ref flag3);
            WriteLiquid(data, tile, ref flag1, ref flag3);
            WriteWires(tile, ref flag2, ref flag3);
            WriteSlopes(tile, ref flag2);
            WriteOther(data, tile, ref flag3, ref flag4);
            WriteFlags(data, ref flag1, ref flag2, ref flag3, ref flag4);
            return tile;
        }

        static void WriteBlock(List<byte> data, ITile tile, ref Flag1 flag1, ref Flag3 flag3)
        {
            if (!tile.active())
            {
                return;
            }
            flag1 |= Flag1.Block;
            var bytes = BitConverter.GetBytes(tile.type);
            data.Add(bytes[0]);

            if (bytes[1] > 0)
            {
                data.Add(bytes[1]);
                flag1 |= Flag1.TwoBytesBlock;
            }
            if (Main.tileFrameImportant[tile.type])
            {
                data.AddRange(BitConverter.GetBytes(tile.frameX));
                data.AddRange(BitConverter.GetBytes(tile.frameY));
            }
            if (tile.color() != 0)
            {
                flag3 |= Flag3.BlockColor;
                data.Add(tile.color());
            }
        }

        static void WriteWall(List<byte> data, ITile tile, ref Flag1 flag1, ref Flag3 flag3)
        {
            if (tile.wall == 0)
            {
                return;
            }
            flag1 |= Flag1.Wall;
            data.Add((byte)tile.wall);

            if (tile.wallColor() > 0)
            {
                flag3 |= Flag3.WallColor;
                data.Add(tile.wallColor());
            }
        }

        static void WriteLiquid(List<byte> data, ITile tile, ref Flag1 flag1, ref Flag3 flag3)
        {
            if (tile.liquid == 0)
            {
                return;
            }
            if (tile.shimmer())
            {
                flag3 |= Flag3.Shimmer;
                flag1 |= Flag1.Water;
            }
            else if (tile.lava())
            {
                flag1 |= Flag1.Lava;
            }
            else if (tile.honey())
            {
                flag1 |= Flag1.Water | Flag1.Lava;
            }
            else
            {
                flag1 |= Flag1.Water;
            }
            data.Add(tile.liquid);
        }

        static void WriteWires(ITile tile, ref Flag2 flag2, ref Flag3 flag3)
        {
            if (tile.wire())
            {
                flag2 |= Flag2.WireR;
            }
            if (tile.wire2())
            {
                flag2 |= Flag2.WireG;
            }
            if (tile.wire3())
            {
                flag2 |= Flag2.WireB;
            }
            if (tile.wire4())
            {
                flag3 |= Flag3.WireY;
            }
            if (tile.actuator())
            {
                flag3 |= Flag3.Actuator;
            }
        }

        static void WriteSlopes(ITile tile, ref Flag2 flag2)
        {
            if (tile.halfBrick())
            {
                flag2 |= Flag2.HalfBrick;
            }
            switch (tile.slope())
            {
                case 1:
                    flag2 |= Flag2.Slope1;
                    break;
                case 2:
                    flag2 |= Flag2.Slope1 | Flag2.HalfBrick;
                    break;
                case 3:
                    flag2 |= Flag2.Slope3;
                    break;
                case 4:
                    flag2 |= Flag2.Slope3 | Flag2.HalfBrick;
                    break;
            }
        }

        static void WriteOther(List<byte> data, ITile tile, ref Flag3 flag3, ref Flag4 flag4)
        {
            if (tile.wall > 255)
            {
                data.Add(BitConverter.GetBytes(tile.wall)[1]);
                flag3 |= Flag3.TwoBytesWall;
            }
            if (tile.invisibleBlock())
            {
                flag4 |= Flag4.InvisibleBlock;
            }
            if (tile.invisibleWall())
            {
                flag4 |= Flag4.InvisibleWall;
            }
            if (tile.fullbrightBlock())
            {
                flag4 |= Flag4.FullBrightBlock;
            }
            if (tile.fullbrightWall())
            {
                flag4 |= Flag4.FullBrightWall;
            }
            if (tile.inActive())
            {
                flag3 |= Flag3.InActive;
            }
        }

        static void WriteFlags(List<byte> data, ref Flag1 flag1, ref Flag2 flag2, ref Flag3 flag3, ref Flag4 flag4)
        {
            if (flag4 != Flag4.None)
            {
                flag3 |= Flag3.Flag4;
                data.Insert(0, (byte)flag4);
            }
            if (flag3 != Flag3.None)
            {
                flag2 |= Flag2.Flag3;
                data.Insert(0, (byte)flag3);
            }
            if (flag2 != Flag2.None)
            {
                flag1 |= Flag1.Flag2;
                data.Insert(0, (byte)flag2);
            }
        }

        enum Flag1 : byte
        {
            None = 0,
            Flag2 = 1,
            Block = 2,
            Wall = 4,
            Water = 8,
            Lava = 16,
            TwoBytesBlock = 32,
            OneByteSame = 64,
            TwoBytesSame = 128,
        }
        enum Flag2 : byte
        {
            None = 0,
            Flag3 = 1,
            WireR = 2,
            WireG = 4,
            WireB = 8,
            HalfBrick = 16,
            Slope1 = 32,
            Slope3 = 64,
        }
        enum Flag3 : byte
        {
            None = 0,
            Flag4 = 1,
            Actuator = 2,
            InActive = 4,
            BlockColor = 8,
            WallColor = 16,
            WireY = 32,
            TwoBytesWall = 64,
            Shimmer = 128,
        }
        enum Flag4 : byte
        {
            None = 0,
            InvisibleBlock = 2,
            InvisibleWall = 4,
            FullBrightBlock = 8,
            FullBrightWall = 16,
        }
    }
}
