/*
    Myna Password Manager Console
    Copyright (C) 2018 Niels Stockfleth

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
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManagerConsole
{
    public class ConsoleReader
    {
        public ConsoleColor Foreground { get; set; } = Console.ForegroundColor;

        public ConsoleColor Background { get; set; } = Console.BackgroundColor;

        public string Prefix { get; set; } = "$>";

        public Func<string, int, Tuple<List<string>, int>> Expand { get; set; } = (cmdline, cmdpos) => Tuple.Create(new List<string>(), 0);

        public List<string> History { get; set; } = new List<string>();

        public Func<char, bool> IsValidChar { get; set; } = (c) => !char.IsControl(c);

        private ConsoleColor originalBackground;

        private ConsoleColor originalForeground;

        public SecureString ReadSecure()
        {
            var ret = new SecureString();
            var sb = ReadLine(true);
            for (int i = 0; i < sb.Length; i++)
            {
                ret.AppendChar(sb[i]);
            }
            return ret;
        }

        public string Read()
        {
            var line = ReadLine(false).ToString().Trim();
            if (line.Length > 0)
            {
                History.Add(line);
            }
            return line;
        }

        private StringBuilder ReadLine(bool secure)
        {
            var txt = new StringBuilder();
            originalForeground = Console.ForegroundColor;
            originalBackground = Console.BackgroundColor;
            Console.Write(Prefix);
            Console.ForegroundColor = Foreground;
            Console.BackgroundColor = Background;
            int pos = 0;
            int historyIndex = History.Count - 1;
            List<string> expand = new List<string>();
            int expandidx = 0;
            int removecnt = 0;
            bool insertMode = true;
            while (true)
            {
                var cki = Console.ReadKey(true);
                if (cki.Key == ConsoleKey.Enter)
                {
                    Console.ForegroundColor = originalForeground;
                    Console.BackgroundColor = originalBackground;
                    Console.WriteLine();
                    return txt;
                }
                if (cki.Key == ConsoleKey.LeftArrow && pos > 0)
                {
                    Console.CursorLeft -= 1;
                    pos--;
                }
                if (cki.Key == ConsoleKey.RightArrow && pos < txt.Length)
                {
                    Console.CursorLeft += 1;
                    pos++;
                }
                if (cki.Key == ConsoleKey.Backspace && pos > 0)
                {
                    RemoveN(txt.Length, txt.Length + 1 - pos);
                    var oldLeft = Console.CursorLeft;
                    for (int p = pos; p < txt.Length; p++)
                    {
                        Console.Write(secure ? '*' : txt[p]);
                    }
                    Console.CursorLeft = oldLeft;
                    pos--;
                    txt.Remove(pos, 1);
                }
                if (cki.Key == ConsoleKey.Delete && pos < txt.Length)
                {
                    RemoveN(txt.Length, txt.Length - pos);
                    var oldLeft = Console.CursorLeft;
                    for (int p = pos + 1; p < txt.Length; p++)
                    {
                        Console.Write(secure ? '*' : txt[p]);
                    }
                    Console.CursorLeft = oldLeft;
                    txt.Remove(pos, 1);
                }
                if (cki.Key == ConsoleKey.End && pos < txt.Length)
                {
                    Console.CursorLeft = txt.Length + Prefix.Length;
                    pos = txt.Length;
                }
                if (cki.Key == ConsoleKey.Home && pos > 0)
                {
                    Console.CursorLeft = Prefix.Length;
                    pos = 0;
                }
                if (cki.Key == ConsoleKey.Insert)
                {
                    insertMode = !insertMode;
                    try { Console.CursorSize = insertMode ? 10 : 50; } catch {/*fails on linux*/}
                }
                if (!secure && (cki.Key == ConsoleKey.UpArrow || cki.Key == ConsoleKey.DownArrow))
                {
                    if (cki.Key == ConsoleKey.UpArrow && historyIndex >= 0 && historyIndex < History.Count)
                    {
                        RemoveN(txt.Length, txt.Length);
                        txt.Clear();
                        txt.Append(History[historyIndex--]);
                        pos = txt.Length;
                        Console.Write(txt);
                    }
                    if (cki.Key == ConsoleKey.DownArrow && historyIndex + 1 < History.Count && History.Count > 0)
                    {
                        RemoveN(txt.Length, txt.Length);
                        txt.Clear();
                        historyIndex += 1;
                        if (historyIndex + 1 < History.Count)
                        {
                            txt.Append(History[historyIndex + 1]);
                        }
                        pos = txt.Length;
                        Console.Write(txt);
                    }
                }
                else
                {
                    historyIndex = History.Count - 1;
                }
                if (!secure && cki.Key == ConsoleKey.Tab)
                {
                    if (expand.Count == 0)
                    {
                        var cmdline = txt.ToString();
                        try
                        {
                            var expandresult = Expand(txt.ToString(), pos - 1);
                            expand = expandresult.Item1;
                            removecnt = txt.Length;
                            if (expandresult.Item2 > 0)
                            {
                                removecnt -= expandresult.Item2;
                            }
                            expandidx = 0;
                        }
                        catch {/*ignored*/}
                    }
                    if (expandidx < expand.Count)
                    {
                        var current = expandidx;
                        if (cki.Modifiers.HasFlag(ConsoleModifiers.Shift))
                        {
                            current = (expandidx + expand.Count - 2) % expand.Count;
                        }
                        RemoveN(txt.Length, removecnt);
                        txt.Remove(txt.Length - removecnt, removecnt);
                        pos -= removecnt;
                        removecnt = expand[current].Length;
                        Console.Write(expand[current]);
                        txt.Append(expand[current]);
                        pos += removecnt;
                        expandidx = (current + 1) % expand.Count;
                    }
                }
                else
                {
                    expandidx = 0;
                    removecnt = 0;
                    expand.Clear();
                }
                if (cki.Modifiers.HasFlag(ConsoleModifiers.Control) && cki.Key == ConsoleKey.K)
                {
                    RemoveN(txt.Length);
                    txt.Clear();
                    pos = 0;
                    historyIndex = History.Count - 1;
                    expand.Clear();
                    expandidx = 0;
                    removecnt = 0;
                }
                if (IsValidChar(cki.KeyChar))
                {
                    Console.Write(secure ? '*' : cki.KeyChar);
                    if (insertMode)
                    {
                        var oldLeft = Console.CursorLeft;
                        for (int p = pos; p < txt.Length; p++)
                        {
                            Console.Write(secure ? '*' : txt[p]);
                        }
                        Console.CursorLeft = oldLeft;
                    }
                    pos = PutChar(txt, pos, cki.KeyChar, insertMode);
                }
            }
        }

        private void RemoveN(int len, int n = -1)
        {
            Console.CursorLeft = len + Prefix.Length;
            Console.BackgroundColor = originalBackground;
            if (n == -1) n = len;
            for (; n > 0; n--)
            {
                Console.Write("\b \b");
            }
            Console.BackgroundColor = Background;
        }

        private int PutChar(StringBuilder txt, int pos, char c, bool insertMode)
        {
            if (pos == txt.Length)
            {
                txt.Append(c);
            }
            else
            {
                if (insertMode)
                {
                    txt.Insert(pos, c);
                }
                else
                {
                    txt[pos] = c;
                }
            }
            return pos + 1;
        }
    }
}
