using System;
using System.Collections.Generic;
using System.IO;

public class FileUtils
{
    public static string RemoveFirstLine(string path)
    {
        string[] lines = File.ReadAllLines(path);
        string firstLine = lines[0];
        using (StreamWriter sw = new StreamWriter(path, false))
        {
            for (int i = 1; i < lines.Length; i++)
            {
                sw.WriteLine(lines[i]);
            }
        }
        return firstLine;
    }
}
