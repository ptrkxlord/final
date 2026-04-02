using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DuckDuckRat.Scripts
{
    class Cleaner
    {
        static void Main()
        {
            var files = Directory.GetFiles(".", "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.Contains("scripts") || file.Contains("defense")) continue;
                string content = File.ReadAllText(file);
                if (content.Contains("// [POLY_JUNK]"))
                {
                    Console.WriteLine("Cleaning: " + file);
                    string cleaned = Regex.Replace(content, @"// \[POLY_JUNK\].*?(\n|\r\n|$)", "", RegexOptions.Singleline);
                    File.WriteAllText(file, cleaned);
                }
            }
        }
    }
}


