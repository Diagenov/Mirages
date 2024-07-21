﻿using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        static int X = 0;
        static int Y = 10;
        static StatusType Type = StatusType.TextShadows | StatusType.TextRight;
        static string Text = "TTT[c/ff00ff:TTT][i:3737]";
        static List<Mirage> mirages = new List<Mirage>();

        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            //ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnInitialize);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(disposing);
        }

        void OnInitialize(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command(Ah, "ah"));
            Commands.ChatCommands.Add(new Command(Oh, "oh"));
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

        async void OnGreet(GreetPlayerEventArgs e)
        {
            var player = TShock.Players[e.Who];
            var list = await Task.Run(() => Mirage.GetSections(player));

            if (list == null || list.Count == 0)
            {
                return;
            }
            foreach (var i in list)
            {
                foreach (var j in i)
                {
                    j.ClearEverything();
                }
                i.Send(false, player);
                await Task.Delay(100);
            }
        }

        void OnLeave(LeaveEventArgs e)
        {
            var player = TShock.Players[e.Who];
            if (player == null || !player.ReceivedInfo)
            {
                return;
            }
            X = 0;
            Y = 9;
            Text = "TTTTTT";
        }

        void Ah(CommandArgs e) // на "тайл" влево
        {
            e.Player.SendInfoMessage($"X: {X}, Y: {Y}, Length: {Text.Length}");
            Hah(e.Player, X, Y, Type, Text, "TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]");
            //X += 10;
            Y += 5;
        }

        void Oh(CommandArgs e)
        {
            e.Player.SendInfoMessage($"X: {X}, Y: {Y}, Length: {Text.Length}");
            Hah(e.Player, X, Y, Type, Text, "TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]TTTTTT[i:3737]");
            Text += "\nTTTTTT[i:3737]";
        }

        void Hah(TSPlayer player, int x, int y, StatusType type, params string[] lines) 
        {
            new StatusText(x, y, type, lines).Spawn(player);
        }
    }

    public enum StatusType : byte //сделать поддержку сразу нескольких панелек текста
    {
        None = 0,
        TextShadows = 2,
        TextLeft = 4,
        TextRight = 8,
        TextCenter = 16,
    }

    public class StatusText
    {
        List<string> list = new List<string>();
        int x;
        int y;
        StatusType type;

        public StatusText(int x, int y, StatusType type, params string[] lines)
        {
            this.x = x * 8;
            this.y = y;
            this.type = type;

            if (lines == null || lines.Length == 0)
            {
                return;
            }
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l))
                {
                    list.Add("");
                    continue;
                }
                list.AddRange(l.Split('\n').Select(i => string.IsNullOrWhiteSpace(i) ? "" : i));
            }
            list.RemoveAll(l => string.IsNullOrWhiteSpace(l));
        }

        public StatusText(params StatusText[] statuses)
        {
            if (statuses == null || statuses.Length == 0 || statuses.All(i => i.list.Count == 0))
            {
                return;
            }
            if (statuses.Length == 1)
            {
                this.list = statuses[0].list;
                x = statuses[0].x;
                y = statuses[0].y;
                type = statuses[0].type;
                return;
            }
            var list = statuses.Select(i => new StatusTextHelp(i)).OrderBy(i => i.y);

            var startY = list.First().y;
            var endY = list.Last().y + list.Last().arr.Length;

            x = statuses.Min(i => i.x);
            y = startY;

            for (int y = startY; y < endY; y++)
            {
                var line = list.Where(i => i.y <= y && y < i.y + i.arr.Length).OrderBy(i => i.x); //все панельки, у которых есть текст на линии y
                var max = line.Max(i => i.arr.Length);

                for (int i = 0; i < max; i++)
                {

                }

                line.Select(i => i.arr[y - i.y]); //все тексты на линии y
            }
        }

        string GetText(out int x, out int y, out int length, out string[] arr)
        {
            arr = list.ToArray();
            x = this.x;
            y = this.y;

            var pureText = "";
            var len = length = arr.Max(l => l.StatusLineLength(out pureText));
            var index = Array.FindIndex(arr, l => len == l.StatusLineLength(out pureText));

            if (length > 47) //правый край экрана
            {
                x = Math.Max(x, length - 47);
            }
            if (x + length > 650) //левый край экрана
            {
                x = 650 - length;
            }
            x = Math.Min(650, Math.Max(x, 0));
            y = Math.Max(0, Math.Min(46 - arr.Length, y));

            if ((8 & (byte)type) == 0 && ((16 | 4) & (byte)type) > 0)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    if (i == index)
                    {
                        continue;
                    }
                    var l = length - arr[i].StatusLineLength(out string s);

                    if ((16 & (byte)type) == 16)
                    {
                        l /= 2;
                    }
                    arr[i] = new string(' ', l) + arr[i];
                }
            }
            if (arr[index] != pureText) //если в начале текста нет цвета, но в самом тексте он есть, то...
            {
                var orig = arr[index];
                var s = "";
                for (int i = 0; i < orig.Length && orig[i] == pureText[i]; i++)
                {
                    s += orig[i];
                }
                x += s.StatusLineLength(out pureText);
            }
            arr[index] += new string(' ', x);
            return 
                new string('\n', y) +
                string.Join("\n", arr);
        }

        public void Spawn(TSPlayer player)
        {
            if (player == null || !player.ConnectionAlive)
            {
                return;
            }
            if (list.Count == 0)
            {
                player.SendData(PacketTypes.Status, "", 0, 1);
                return;
            }
            player.SendData(PacketTypes.Status, GetText(out int x, out int y, out int length, out string[] arr), 0, ((byte)type & 2) | 1);
        }
    
        struct StatusTextHelp
        {
            public int x;
            public int y;
            public int length;
            public string[] arr;

            public StatusTextHelp(StatusText i)
            {
                i.GetText(out x, out y, out length, out arr);
            }
        }
    }

    public static class HelpHelp
    {
        public static int StatusLineLength(this string text, out string pureText)
        {
            if (text == "")
            {
                pureText = "";
                return 0;
            }
            var orig = text;
            
            text = Regex.Replace(text, @"\[c[^:]*:([^]]*)\]", "$1", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\[[inag](?:\/[sp]\d+){0,2}:\d+\]", "——", RegexOptions.IgnoreCase);

            if ((pureText = text) == "")
            {
                return 0;
            }
            var oneCount = 0;
            var twoCount = 0;
            var threeCount = 0;

            foreach (var i in text) //стоимость символов считается в знаках табуляции
            {
                if (i == ' ')
                {
                    oneCount++;
                }
                else if (i == '—')
                {
                    threeCount++;
                }
                else
                {
                    twoCount++;
                }
            }
            return oneCount + threeCount + twoCount + ((twoCount + threeCount) / 2) + (twoCount / 15) + (threeCount / 5); 
        }
    }
}