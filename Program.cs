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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PasswordManagerConsole
{
    public class Program
    {
        private static List<string> Commands = new List<string> {
            "Clear-Console",
            "Exit-Console",
            "List-Account",
            "Open-Repository",
            "Open-URL",
            "Show-Account",
            "Show-Help",
            "Show-License"
        };

        private static PasswordRepository repository = null;

        public static void Main(string[] args)
        {
            ShowHelpCommand();
            var consoleReader = new ConsoleReader();
            consoleReader.Background = ConsoleColor.DarkBlue;
            consoleReader.Foreground = ConsoleColor.Yellow;
            consoleReader.Expand = Expand;
            while (true)
            {
                try
                {
                    if (!DispatchCommand(consoleReader))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print("Failed to dispatch command. Reason: {0}", ex.Message);
                }
            }
        }

        private static bool DispatchCommand(ConsoleReader consoleReader)
        {
            var ret = true;
            var parseResult = Shell.Parse(consoleReader.Read());
            if (parseResult.Count > 0 && parseResult[0].InputToken == Shell.Token.ID)
            {
                switch (parseResult[0].Input.ToLowerInvariant())
                {
                    case "open-repository":
                        OpenRepositoryCommand(parseResult);
                        break;
                    case "list-account":
                        ListAccountCommand(parseResult);
                        break;
                    case "show-account":
                        ShowAccountCommand(parseResult);
                        break;
                    case "open-url":
                        OpenURLCommand(parseResult);
                        break;
                    case "exit-console":
                        ret = false;
                        break;
                    case "clear-console":
                        ClearConsoleCommand();
                        break;
                    case "show-help":
                        ShowHelpCommand();
                        break;
                    case "show-license":
                        ShowLicenseCommand();
                        break;
                    default:
                        Console.WriteLine("Invalid command. Type Show-Help to see all available commands.");
                        break;
                }
            }
            return ret;
        }

        private static Tuple<List<string>, int> Expand(string cmdline, int cmdpos)
        {
            var ret = new List<string>();
            var result = Shell.Parse(cmdline);
            var match = Commands;
            var cmp = "";
            int pos = 0;
            if (result.Count > 0)
            {
                cmp = result[0].Input.ToLowerInvariant();
                pos = result[0].StartPosition;
                foreach (var c in Commands)
                {
                    if (string.Equals(c, cmp, StringComparison.InvariantCultureIgnoreCase))
                    {
                        match = new List<string>();
                        break;
                    }
                }
                if (cmp == "show-account" || cmp == "open-url")
                {
                    if (repository != null && result.Count < 3 && cmdpos >= cmp.Length)
                    {
                        cmp = result.Count == 2 ? result[1].Input.ToLowerInvariant() : "";
                        pos = result.Count == 2 ? result[1].StartPosition : result[0].EndPosition + 1;
                        if (pos <= cmdline.Length)
                        {
                            foreach (var p in repository.Passwords)
                            {
                                match.Add(p.Name);
                            }
                            match.Sort();
                        }
                    }
                }
                if (cmp == "open-repository" && (
                        result.Count < 3 ||
                        result.Count == 3 && // special case: './a b/'a should be expanded
                        result[1].InputToken == Shell.Token.STRING &&
                        result[2].StartPosition == result[1].EndPosition + 1)
                    && cmdpos >= cmp.Length)
                {
                    var fileName = "";
                    if (result.Count == 2)
                    {
                        fileName = result[1].Input;
                    }
                    else if (result.Count == 3)
                    {
                        fileName = $"{result[1].Input}{result[2].Input}";
                    }
                    ret = Shell.ExpandFiles(fileName);
                    pos = result[0].EndPosition + 1;
                }
            }
            foreach (var c in match)
            {
                if (c.StartsWith(cmp, StringComparison.InvariantCultureIgnoreCase))
                {
                    ret.Add(Shell.Quote(c));
                }
            }
            return Tuple.Create(ret, pos);
        }

        private static void ShowHelpCommand()
        {
            Console.WriteLine("Myna Password Manager Console");
            Console.WriteLine("  Provides read access to a Myna password repository.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  Clear-Console             - Clears the console.");
            Console.WriteLine("  Exit-Console              - Exits the program.");
            Console.WriteLine("  List-Account [<filter>]   - Lists password accounts.");
            Console.WriteLine("  Open-Repository <pwdfile> - Opens a password repository.");
            Console.WriteLine("  Open-URL <account>        - Opens the account's URL in a browser.");
            Console.WriteLine("  Show-Account <account>    - Displays account information.");
            Console.WriteLine("  Show-Help                 - Displays this text.");
            Console.WriteLine("  Show-License              - Displays license information.");
        }

        private static void ShowLicenseCommand()
        {
            Console.WriteLine("Myna Password Manager Console");
            Console.WriteLine("Copyright (C) 2018 Niels Stockfleth");
            Console.WriteLine();
            Console.WriteLine("This program is free software: you can redistribute it and/or modify");
            Console.WriteLine("it under the terms of the GNU General Public License as published by");
            Console.WriteLine("the Free Software Foundation, either version 3 of the License, or");
            Console.WriteLine("(at your option) any later version.");
            Console.WriteLine();
            Console.WriteLine("This program is distributed in the hope that it will be useful,");
            Console.WriteLine("but WITHOUT ANY WARRANTY; without even the implied warranty of");
            Console.WriteLine("MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the");
            Console.WriteLine("GNU General Public License for more details.");
            Console.WriteLine();
            Console.WriteLine("You should have received a copy of the GNU General Public License");
            Console.WriteLine("along with this program.  If not, see <http://www.gnu.org/licenses/>.");
        }

        private static void ClearConsoleCommand()
        {
            Console.Clear();
        }

        private static void OpenRepositoryCommand(List<Shell.ParseResult> result)
        {
            if (result.Count < 2)
            {
                Console.WriteLine("Missing <pwdfile> argument.");
                return;
            }
            if (!File.Exists(result[1].Input))
            {
                Console.WriteLine("Password repository file does not exist.");
                return;
            }
            var absfilename = Path.GetFullPath(result[1].Input);
            var dirname = Path.GetDirectoryName(absfilename);
            var cs = new ConsoleReader();
            cs.Prefix = "Enter password:";
            var pwd = cs.ReadSecure();
            try
            {
                repository = PasswordRepository.Read(absfilename, dirname, pwd, false);
            }
            catch
            {
                Console.WriteLine("Access denied.");
            }
        }

        private static void ListAccountCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            var sorted = new List<Password>(repository.Passwords);
            sorted.Sort((p1, p2) => p1.Name.CompareTo(p2.Name));
            foreach (var password in sorted)
            {
                if (result.Count < 2 || password.Name.StartsWith(result[1].Input, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (password.Name.IndexOf(' ') > 0)
                    {
                        Console.WriteLine($"'{password.Name}'");
                    }
                    else
                    {
                        Console.WriteLine(password.Name);
                    }
                }
            }
        }

        private static void OpenURLCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            if (result.Count < 2)
            {
                Console.WriteLine("Missing <account> argument.");
                return;
            }
            foreach (var p in repository.Passwords)
            {
                if (string.Equals(p.Name, result[1].Input, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(p.Url))
                    {
                        Shell.OpenURL(p.Url);
                    }
                    else
                    {
                        Console.WriteLine("No URL has been configured for the account.");
                    }
                    break;
                }
            }
        }

        private static void ShowAccountCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            if (result.Count < 2)
            {
                Console.WriteLine("Missing <account> argument.");
                return;
            }
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            foreach (var p in repository.Passwords)
            {
                if (string.Equals(p.Name, result[1].Input, StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine($"--- {p.Name} ---");
                    Console.WriteLine($"URL:{p.Url}");
                    Console.WriteLine($"Login:{p.Login}");
                    Console.WriteLine($"Password:{p.SecurePassword.GetAsString()}");
                    Console.WriteLine($"Description:{p.Description}");
                    break;
                }
            }
        }
    }
}
