using FrameGenerator.Models;
using ICSharpCode.SharpZipLib.Zip;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DCSSTV
{

    public class ReadFromFile
    {
        public  Dictionary<string, string> GetDictionaryFromFile(string path)
        {
            try
            {
                var dict = new Dictionary<string, string>();
                var client = new WebClient();
                var url = new Uri ("https://github.com/Aspectrus/DCSSExtraFiles/race.png");
                var textFromFile = client.DownloadString("https://github.com/Aspectrus/DCSSExtraFiles/race.png");
                //  byte[] bytes = Encoding.Default.GetBytes(textFromFile);
                //  textFromFile = Encoding.UTF8.GetString(bytes);
                Console.WriteLine(textFromFile.Length);
                string[] lines = textFromFile.Split(
                new[] { "\r\n", "\r", "\n" },
                 StringSplitOptions.None);
               
                   for (var i = 0; i < lines.Length; i += 2)
                     {
                        dict[lines[i]] = lines[i + 1];
                     }
                return dict;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Heelo");
                while (ex != null)
                {
                    Console.WriteLine(ex.Message);
                    ex = ex.InnerException;
                }
                return null;
            }

        }
    }
}