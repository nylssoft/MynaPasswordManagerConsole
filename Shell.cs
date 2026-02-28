/*
    Myna Password Manager Console
    Copyright (C) 2018-2026 Niels Stockfleth

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MynaPasswordManagerConsole
{
    public static class Shell
    {
        public enum Token { ID, STRING };

        public class ParseResult(int startpos, int endpos, Shell.Token token, string input)
        {
            public int StartPosition { get; set; } = startpos;

            public int EndPosition { get; set; } = endpos;

            public string Input { get; set; } = input;

            public Token InputToken { get; set; } = token;
        }

        public static List<ParseResult> Parse(string cmdline)
        {
            var ret = new List<ParseResult>();
            var arg = new StringBuilder();
            var instring = false;
            var stringchar = '"';
            int idx = -1;
            int startidx = -1;
            foreach (var c in cmdline)
            {
                idx++;
                if (c == '"' || c == '\'')
                {
                    if (instring && c == stringchar)
                    {
                        ret.Add(new ParseResult(startidx, idx, Token.STRING, arg.ToString()));
                        arg.Clear();
                        startidx = -1;
                        continue;
                    }
                    if (!instring)
                    {
                        startidx = idx;
                        instring = true;
                        stringchar = c;
                        continue;
                    }
                }
                if (c == ' ' && !instring)
                {
                    if (arg.Length > 0)
                    {
                        ret.Add(new ParseResult(startidx, idx, Token.ID, arg.ToString()));
                        arg.Clear();
                        startidx = -1;
                    }
                    continue;
                }
                if (instring || c != ' ')
                {
                    if (startidx < 0)
                    {
                        startidx = idx;
                    }
                    arg.Append(c);
                }
            }
            if (arg.Length > 0)
            {
                ret.Add(new ParseResult(startidx, idx, instring ? Token.STRING : Token.ID, arg.ToString()));
            }
            return ret;
        }

        public static string Quote(string input)
        {
            if (input.Contains(' '))
            {
                return $"'{input}'";
            }
            return input;
        }

        public static List<string> ExpandFiles(string fileName)
        {
            var ret = new List<string>();
            try
            {
                var dirName = "";
                var searchPattern = "";
                var dirPrefix = "";
                if (Directory.Exists(fileName))
                {
                    dirName = fileName;
                    if (!fileName.EndsWith(Path.DirectorySeparatorChar))
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            && fileName.EndsWith('/'))
                        {
                            dirName = dirName[..^1];
                        }
                        dirName += Path.DirectorySeparatorChar;
                    }
                    dirPrefix = dirName;
                }
                else if (!File.Exists(fileName))
                {
                    var idx = fileName.LastIndexOf(Path.DirectorySeparatorChar);
                    if (idx < 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        idx = fileName.LastIndexOf('/');
                    }
                    if (idx >= 0)
                    {
                        var d = fileName[..(idx + 1)];
                        if (Directory.Exists(d))
                        {
                            dirName = d;
                            dirPrefix = dirName;
                            if (idx < fileName.Length - 1)
                            {
                                searchPattern = $"{fileName[(idx + 1)..]}*";
                            }
                        }
                    }
                    else
                    {
                        dirName = Directory.GetCurrentDirectory();
                        dirPrefix = $".{Path.DirectorySeparatorChar}";
                        searchPattern = $"{fileName}*";
                    }
                }
                if (!string.IsNullOrEmpty(dirName))
                {
                    foreach (var file in Directory.EnumerateFiles(dirName, searchPattern))
                    {
                        var e = $"{dirPrefix}{Path.GetFileName(file)}";
                        ret.Add(Quote(e));
                    }
                    foreach (var file in Directory.EnumerateDirectories(dirName, searchPattern))
                    {
                        var e = $"{dirPrefix}{Path.GetFileName(file)}{Path.DirectorySeparatorChar}";
                        ret.Add(Quote(e));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Failed to expand filename '{0}'. Reason: {1}.", fileName, ex.Message);
            }
            return ret;
        }

        public static void OpenURL(string url)
        {
            try
            {
                if (!url.StartsWith("http"))
                {
                    url = $"https://{url}";
                }
                Process.Start(url);
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}