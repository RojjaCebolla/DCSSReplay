﻿using InputParse;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace FrameGenerator.FileReading
{
    public static class ReadFromFile
    {
        public static Dictionary<string, string> GetDictionaryFromFile(string path)
        {
            var dict = new Dictionary<string, string>();

            string[] lines = File.ReadAllLines(path);

            for (var i = 0; i < lines.Length; i += 2)
            {
                dict[lines[i]] = lines[i + 1];
            }
            return dict;
        }

        public static Dictionary<string, string> GetMonsterData(string file, string monsterOverrideFile)
        {
            var monster = new Dictionary<string, string>();

            string[] lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("  MONS_"))
                {
                    string[] tokens = lines[i].Split(',');
                    tokens[1] = tokens[1].Replace("'", "").Replace(" ", "");
                    tokens[2] = tokens[2].Replace(" ", "");
                    tokens[0] = tokens[0].Replace("MONS_", "").Replace(" ", "").ToLower();
                    //if(!Enum.TryParse(tokens[2], out ColorList2 res)) Console.WriteLine(tokens[1] + tokens[2] + " badly colored: " + tokens[0]);
                    if(monster.TryGetValue(tokens[1] + tokens[2], out var existing)) { 
                        //Console.WriteLine(tokens[1] + tokens[2] + "exist: " + existing + " new: " + tokens[0]); 
                    }
                    else monster[tokens[1] + tokens[2]] = tokens[0];
                }
            }

            //Overrides for duplicates, others handled by name from monster log

            lines = File.ReadAllLines(monsterOverrideFile);

            foreach (var line in lines)
            {
                var keyValue = line.Split(' ');
                monster[keyValue[0]] = keyValue[1];
            }

            return monster;
        }

        public static List<NamedMonsterOverride> GetNamedMonsterOverrideData(string monsterOverrideFile)
        {
            var monster = new List<NamedMonsterOverride>();

            string[] lines = File.ReadAllLines(monsterOverrideFile);

            var name = "";
            var location = "";
            var tileNameOverrides = new Dictionary<string, string>(20);

            bool pngParse = false;

            for (var i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    monster.Add(new NamedMonsterOverride(name, location, tileNameOverrides));
                    name = "";
                    location = "";
                    tileNameOverrides = new Dictionary<string, string>(20);
                    pngParse = false; 
                    continue; 
                }
                if (pngParse)
                {
                    string[] tokens = lines[i].Split(' ');
                    tileNameOverrides.Add(tokens[0], tokens[1]);
                }
                else
                {
                    string[] tokens = lines[i].Split(';');
                    name = tokens[0];
                    location = tokens.Length > 1 ? tokens[1] : "";
                    pngParse = true;
                }

            }

            return monster;
        }

        public static Dictionary<string, string[]> GetFloorAndWallNamesForDungeons(string file)
        {

            var floorandwall = new Dictionary<string, string[]>();
            string[] lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i += 3)
            {
                string[] temp = new string[2];
                temp[0] = lines[i + 1];
                temp[1] = lines[i + 2];
                floorandwall[lines[i].ToUpper()] = temp;
            }

            return floorandwall;
        }
        
        public static Dictionary<string, Bitmap> GetBitmapDictionaryFromFolder(string folder)
        {
            var dict = new Dictionary<string, Bitmap>();
            List<string> pngFiles = Directory.GetFiles(folder, "*.png*", SearchOption.AllDirectories).ToList();
            var files = Directory.GetFiles(@"..\..\..\Extra", "*.png", SearchOption.TopDirectoryOnly).ToList();
            pngFiles.AddRange(files);
            foreach (var file in pngFiles)
            {
                FileInfo info = new FileInfo(file);
                Bitmap bitmap = new Bitmap(file);
                dict[info.Name.Replace(".png", "")] = bitmap;
            }
            return dict;
        }
    
        public static Dictionary<string, Bitmap> GetCharacterPNG(string gameLocation)
        {

            var GetCharacterPNG = new Dictionary<string, Bitmap>();

            List<string> allpngfiles = Directory.GetFiles(gameLocation + @"\rltiles\player\base", "*.png*", SearchOption.AllDirectories).ToList();
            allpngfiles.AddRange(Directory.GetFiles(gameLocation + @"\rltiles\player\felids", "*.png*", SearchOption.AllDirectories).ToList());
            foreach (var file in allpngfiles)
            {
                FileInfo info = new FileInfo(file);
                Bitmap bitmap = new Bitmap(file);


                GetCharacterPNG[info.Name.Replace(".png", "")] = bitmap;

            }
            return GetCharacterPNG;
        }

        public static Dictionary<string, Bitmap> GetMonsterPNG(string gameLocation)
        {

            var monsterPNG = new Dictionary<string, Bitmap>();
            string[] allpngfiles = Directory.GetFiles(gameLocation + @"\rltiles\mon", "*.png*", SearchOption.AllDirectories);
            foreach (var file in allpngfiles)
            {
                FileInfo info = new FileInfo(file);
                Bitmap bitmap = new Bitmap(file);
                monsterPNG[info.Name.Replace(".png", "")] = bitmap;

            }
            Bitmap bmp = new Bitmap(gameLocation + @"\rltiles\dngn\statues\statue_triangle.png");
            monsterPNG["roxanne"] = bmp;
            return monsterPNG;
        }
    }
}
