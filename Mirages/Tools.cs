using System.IO;
using Microsoft.Xna.Framework;

namespace Mirages
{
    public static class Tools
    {
        public static Point ReadPoint16(this BinaryReader r)
        {
            return new Point
            {
                X = r.ReadInt16(),
                Y = r.ReadInt16(),
            };
        }

        public static Data32Packet Read32Packet(this BinaryReader r)
        {
            var chestID = r.ReadInt16();
            var slotID = r.ReadByte();
            var stack = r.ReadInt16();
            var prefix = r.ReadByte();
            var netID = r.ReadInt16();

            return new Data32Packet
            {
                ChestID = chestID,
                Item = new SlotItem(slotID, netID, stack, prefix),
            };
        }

        public static Data33Packet Read33Packet(this BinaryReader r)
        {
            var chestID = r.ReadInt16();
            var x = r.ReadInt16();
            var y = r.ReadInt16();
            var nameLength = r.ReadByte();

            var name = "";
            if (nameLength > 0 && nameLength <= 20)
            {
                name = r.ReadString();
            }
            return new Data33Packet
            {
                ChestID = chestID,
                X = x,
                Y = y,
                NameLength = nameLength,
                Name = name,
            };
        }

        public static Data47Packet Read47Packet(this BinaryReader r)
        {
            return new Data47Packet
            {
                SignID = r.ReadInt16(),
                X = r.ReadInt16(),
                Y = r.ReadInt16(),
                Text = r.ReadString(),
                PlayerID = r.ReadByte(),
                TBD = r.ReadBoolean(),
            };
        }

        public static Data69Packet Read69Packet(this BinaryReader r)
        {
            return new Data69Packet
            {
                ChestID = r.ReadInt16(),
                X = r.ReadInt16(),
                Y = r.ReadInt16(),
            };
        }
    }

    public struct Data32Packet
    {
        public short ChestID;
        public SlotItem Item;
    }

    public struct Data33Packet
    {
        public short ChestID;
        public short X;
        public short Y;
        public byte NameLength;
        public string Name;
    }

    public struct Data69Packet
    {
        public short ChestID;
        public short X;
        public short Y;
    }

    public struct Data47Packet
    {
        public short SignID;
        public short X;
        public short Y;
        public string Text;
        public byte PlayerID;
        public bool TBD;
    }
}

//аналогично для сундуков

//также для сундуков стоит добавить фантомное взаимодействие...

//добавить ли фантомное взаимодействие табличкам???

//добавить метод получения оригинала (то, что в действительности на месте фантомной области)
