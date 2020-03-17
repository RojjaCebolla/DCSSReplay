﻿using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace InputParse
{
    public class Parser
    {
        private static char GetCharacter(Putty.TerminalCharacter character)
        {
            return character.Character == 55328 ? ' ' : character.Character;
        }
        private static string GetColoredCharacter(Putty.TerminalCharacter character)
        {
            return GetCharacter(character) + Enum.GetName(typeof(ColorList2), character.ForegroundPaletteIndex);
        }
        private static string GetBackgroundColor(Putty.TerminalCharacter character)
        {
            return Enum.GetName(typeof(ColorList2), character.BackgroundPaletteIndex);
        }
        public static LayoutType GetLayoutType(Putty.TerminalCharacter[,] characters, out string newlocation)
        {
            StringBuilder place = new StringBuilder();
            bool found = false;

            string sideLocation;
            newlocation = "";
            for (int i = 61; i < FullWidth; i++)
            {
                place.Append(GetCharacter(characters[i, 7]));
            }
            sideLocation = place.ToString();
            foreach (var location in Locations.locations)
            {
                if (sideLocation.Contains(location.Substring(0, 3)))
                {
                    newlocation = location;
                    sideLocation = location;
                    found = true;
                    break;
                }
            }
            if (found)
            {
                return LayoutType.Normal;
            }
            place = new StringBuilder();
            string mapLocation;
            for (int i = 0; i < 30; i++)
            {
                place.Append(GetCharacter(characters[i, 0]));
            }
            mapLocation = place.ToString();
            if (!mapLocation.Contains("of")) return LayoutType.TextOnly;
            foreach (var location in Locations.locations)
            {
                if (mapLocation.Contains(location.Substring(0, 3)))
                {
                    newlocation = location;
                    mapLocation = location;
                    found = true;
                    break;
                }
            }
            if (found)
            {
                return LayoutType.MapOnly;
            }
            return LayoutType.TextOnly;
        }

        const int FullWidth = 80;
        const int AlmostFullWidth = 75;
        const int FullHeight = 24;
        const int GameViewWidth = 33;
        const int GameViewHeight = 17;
        public static Model ParseData(Putty.TerminalCharacter[,] chars)
        {
            var characters = chars;
            if (characters == null) return null;

            var layout = GetLayoutType(characters, out var location);
            switch (layout)
            {
                case LayoutType.Normal:
                    return parseNormalLayout(characters);
                case LayoutType.TextOnly:
                    return parseTextLayout(characters);
                case LayoutType.MapOnly:
                    return parseMapLayout(characters, location);
            }
            return new Model();
        }

        private static Model parseNormalLayout(Putty.TerminalCharacter[,] characters)
        {
            Model model = new Model();
            model.Layout = LayoutType.Normal;
            model.LineLength = GameViewWidth;
            var coloredStrings = new string[model.LineLength * GameViewHeight];
            var highlightColorStrings = new string[model.LineLength * GameViewHeight];
            var curentChar = 0;
            try
            {

                for (int j = 0; j < GameViewHeight; j++)
                    for (int i = 0; i < model.LineLength; i++)
                    {
                        coloredStrings[curentChar] = GetColoredCharacter(characters[i, j]);
                        highlightColorStrings[curentChar] = Enum.GetName(typeof(ColorList2), characters[i, j].BackgroundPaletteIndex);
                        curentChar++;
                    }
                model.TileNames = coloredStrings;

                model.SideData = ParseSideData(characters);
                
                model.LogData = ParseLogLines(characters); 

                model.MonsterData = ParseMonsterDisplay(characters);
            }
            catch (Exception)
            {
                foreach (var item in characters)
                {
                    if (item.ForegroundPaletteIndex > 15) Console.WriteLine(item.ForegroundPaletteIndex + item.ForegroundPaletteIndex);
                }

                return new Model();
            }
            return model;
        }

        private static LogData[] ParseLogLines(Putty.TerminalCharacter[,] characters)
        {
            var loglines = new LogData[6] { new LogData(), new LogData(), new LogData(), new LogData(), new LogData(), new LogData() };
            StringBuilder logLine = new StringBuilder();
            var logText = new List<string>();
            var logBackground = new List<string>();
            var loglineRow = 17;
            foreach (var line in loglines)
            {
                for (int i = 0; i < FullWidth; i++)
                {
                    logLine.Append(GetCharacter(characters[i, loglineRow]));
                }
                line.LogTextRaw = logLine.ToString();
                if (line.LogTextRaw.Length > 0)
                {
                    line.empty = false;
                    for (int i = 0; i < line.LogTextRaw.Length; i++)
                    {
                        logText.Add(GetColoredCharacter(characters[i, loglineRow]));
                        logBackground.Add(GetBackgroundColor(characters[i, loglineRow]));
                    }
                    line.LogText = logText.ToArray();
                    line.LogBackground = logBackground.ToArray();
                    logText.Clear();
                    logBackground.Clear();
                }
                logLine.Clear();
                loglineRow++;
            }
            return loglines;
        }

        private static MonsterData[] ParseMonsterDisplay(Putty.TerminalCharacter[,] characters)
        {
            StringBuilder monsterLine1 = new StringBuilder();
            StringBuilder monsterLine2 = new StringBuilder();
            StringBuilder monsterLine3 = new StringBuilder();
            StringBuilder monsterLine4 = new StringBuilder();
            string[] monsterLine1Colored = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine2Colored = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine3Colored = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine4Colored = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine1Background = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine2Background = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine3Background = new string[AlmostFullWidth - GameViewWidth - 4];
            string[] monsterLine4Background = new string[AlmostFullWidth - GameViewWidth - 4];
            int currentChar = 0;
            for (int i = GameViewWidth + 4; i < AlmostFullWidth; i++, currentChar++)
            {
                monsterLine1.Append(GetCharacter(characters[i, 13]));
                monsterLine2.Append(GetCharacter(characters[i, 14]));
                monsterLine3.Append(GetCharacter(characters[i, 15]));
                monsterLine4.Append(GetCharacter(characters[i, 16]));
                monsterLine1Colored[currentChar] = GetColoredCharacter(characters[i, 13]);
                monsterLine2Colored[currentChar] = GetColoredCharacter(characters[i, 14]);
                monsterLine3Colored[currentChar] = GetColoredCharacter(characters[i, 15]);
                monsterLine4Colored[currentChar] = GetColoredCharacter(characters[i, 16]);
                monsterLine1Background[currentChar] = GetBackgroundColor(characters[i, 13]);
                monsterLine2Background[currentChar] = GetBackgroundColor(characters[i, 14]);
                monsterLine3Background[currentChar] = GetBackgroundColor(characters[i, 15]);
                monsterLine4Background[currentChar] = GetBackgroundColor(characters[i, 16]);
            }
            
            return new MonsterData[4] { 
                FormatMonsterData(monsterLine1.ToString(), monsterLine1Colored, monsterLine1Background),
                FormatMonsterData(monsterLine2.ToString(), monsterLine2Colored, monsterLine2Background),
                FormatMonsterData(monsterLine3.ToString(), monsterLine3Colored, monsterLine3Background),
                FormatMonsterData(monsterLine4.ToString(), monsterLine4Colored, monsterLine4Background) };
        }

        private static MonsterData FormatMonsterData(string monsterLine, string[] monsterLineColored, string[] monsterBackgroundColors)
        {
            if (monsterLine[0].Equals(' '))
            {
                return new MonsterData();
            }
            var chars = new char[] { ' ' };
            var split = monsterLine.ToString().Split(chars, count: 2);
            return new MonsterData() { 
                empty = false,
                MonsterTextRaw = split[1],
                MonsterDisplay = monsterLineColored.Take(split[0].Length).ToArray(),
                MonsterText = monsterLineColored.Skip(split[0].Length).ToArray(),
                MonsterBackground = monsterBackgroundColors
            };
        }

        private static Model parseMapLayout(Putty.TerminalCharacter[,] characters, string location)
        {
            Model model = new Model();
            model.Layout = LayoutType.MapOnly;
            model.LineLength = FullWidth;
            var coloredStrings = new string[model.LineLength * FullHeight];
            var curentChar = 0;
            try
            {

                for (int j = 0; j < FullHeight; j++)
                    for (int i = 0; i < model.LineLength; i++)
                    {
                        coloredStrings[curentChar] = GetColoredCharacter(characters[i, j]);
                        curentChar++;
                    }
                model.TileNames = coloredStrings;

                model.SideData = new SideData();
                model.SideData.Place = location;

            }
            catch (Exception)
            {
                foreach (var item in characters)
                {
                    if (item.ForegroundPaletteIndex > 15) Console.WriteLine(item.ForegroundPaletteIndex + item.ForegroundPaletteIndex);
                }

                return new Model();
            }
            return model;
        }

        private static Model parseTextLayout(Putty.TerminalCharacter[,] characters)
        {
            Model model = new Model();
            model.Layout = LayoutType.TextOnly;
            model.LineLength = FullWidth;
            var coloredStrings = new string[model.LineLength * FullHeight];
            var curentChar = 0;
            try
            {
                for (int j = 0; j < FullHeight; j++)
                    for (int i = 0; i < model.LineLength; i++)
                    {
                        coloredStrings[curentChar] = GetColoredCharacter(characters[i, j]);
                        curentChar++;
                    }
                model.TileNames = coloredStrings;

            }
            catch (Exception)
            {
                foreach (var item in characters)
                {
                    if (item.ForegroundPaletteIndex > 15) Console.WriteLine(item.ForegroundPaletteIndex + item.ForegroundPaletteIndex);
                }

                return new Model();
            }
            return model;
        }


        private static SideData ParseSideData(Putty.TerminalCharacter[,] characters)
        {
            var sideData = new SideData();
            StringBuilder name = new StringBuilder();
            StringBuilder race = new StringBuilder();
            StringBuilder weapon = new StringBuilder();
            StringBuilder quiver = new StringBuilder();
            StringBuilder status = new StringBuilder();
            StringBuilder status2 = new StringBuilder();
            StringBuilder ac = new StringBuilder();
            StringBuilder ev = new StringBuilder();
            StringBuilder sh = new StringBuilder();
            StringBuilder xl = new StringBuilder();
            StringBuilder str = new StringBuilder();
            StringBuilder @int = new StringBuilder();
            StringBuilder dex = new StringBuilder();
            StringBuilder hp = new StringBuilder();
            StringBuilder mp = new StringBuilder();
            StringBuilder place = new StringBuilder();
            StringBuilder time = new StringBuilder();
            StringBuilder next = new StringBuilder();
            for (int i = 37; i < 75; i++)
            {
                name.Append(GetCharacter(characters[i, 0]));
                race.Append(GetCharacter(characters[i, 1]));
                weapon.Append(GetCharacter(characters[i, 9]));
                quiver.Append(GetCharacter(characters[i, 10]));
                status.Append(GetCharacter(characters[i, 11]));
                status2.Append(GetCharacter(characters[i, 12]));
            }
            for (int i = 40; i < 44; i++)
            {
                ac.Append(GetCharacter(characters[i, 4]));
                ev.Append(GetCharacter(characters[i, 5]));
                sh.Append(GetCharacter(characters[i, 6]));
                xl.Append(GetCharacter(characters[i, 7]));
                next.Append(GetCharacter(characters[i+10, 7]));
            }
            for (int i = 59; i < 63; i++)
            {
                str.Append(GetCharacter(characters[i, 4]));
                @int.Append(GetCharacter(characters[i, 5]));
                dex.Append(GetCharacter(characters[i, 6]));

            }
            for (int i = 37; i < 52; i++)
            {
                hp.Append(GetCharacter(characters[i + 1, 2]));
                mp.Append(GetCharacter(characters[i, 3]));

            }
            for (int i = 60; i < 75; i++)
            {
                place.Append(GetCharacter(characters[i + 1, 7]));
                time.Append(GetCharacter(characters[i, 8]));

            }

            var splithp = hp.ToString().Split(':').Length > 1 ? hp.ToString().Split(':')[1].Split('/') : new string[] { "1", "1" };
            var splitmp = mp.ToString().Split(':').Length > 1 ? mp.ToString().Split(':')[1].Split('/') : new string[] { "1", "1" };


            if (splithp.Length > 1)
            {
                sideData.Health = int.Parse(splithp[0]);
                var truehp = splithp[1].Split(' ');
                sideData.MaxHealth = int.Parse(truehp[0]);
                sideData.TrueHealth = truehp.Length > 1 ? truehp[1] : "";
            }
            if (splitmp.Length > 1)
            {
                sideData.Magic = int.Parse(splitmp[0]);
                var truemp = splitmp[1].Split(' ');
                sideData.MaxMagic = int.Parse(truemp[0]);
                sideData.TrueHealth = truemp.Length > 1 ? truemp[1] : "";

            }
            sideData.Name = name.ToString();
            sideData.Race = race.ToString();
            sideData.Weapon = weapon.ToString();
            sideData.Quiver = quiver.ToString();
            sideData.Statuses1 = status.ToString();
            sideData.Statuses2 = status2.ToString();
            sideData.ArmourClass = ac.ToString();
            sideData.Evasion = ev.ToString();
            sideData.Shield = sh.ToString();
            sideData.ExperienceLevel = xl.ToString();
            sideData.Strength = str.ToString();
            sideData.Inteligence = @int.ToString();
            sideData.Dexterity = dex.ToString();
            sideData.Place = place.ToString().Trim();
            sideData.Time = time.ToString();
            sideData.NextLevel = next.ToString();

            var parsed = sideData.Place.Split(':');
            bool found = false;
            foreach (var location in Locations.locations)
            {
                if (parsed[0].Contains(location.Substring(0, 3)))
                {
                    sideData.Place = location;
                    found = true;
                    break;
                }
            }
            if (found && parsed.Length>1)
            {
                sideData.Place += ":" + parsed[1];
            }
            return sideData;
        }
    }
}
