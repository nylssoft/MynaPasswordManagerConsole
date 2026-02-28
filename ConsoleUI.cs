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
using System.Linq;
using System.IO;
using System.Globalization;
using System.Security;
using CloudExport;
using CloudExport.Services;

namespace MynaPasswordManagerConsole
{
    public class ConsoleUI
    {
        private enum Answer { Yes, No, Cancel };

        private PasswordRepository repository = null;
        private string repositoryFileName;
        private string keyDirectory;
        private SecureString repositoryPassword;

        private static readonly List<string> Commands = [
            "List-Account",
            "Add-Account",
            "Edit-Account",
            "Delete-Account",
            "Show-Account",
            "Open-URL",
            "New-Repository",
            "Open-Repository",
            "Show-Repository",
            "Edit-Repository",
            "Save-Repository",
            "Close-Repository",
            "Change-MasterPassword",
            "Clear-Console",
            "Exit-Console",
            "Show-Help",
            "Show-License",
            "Sync-Passwords"
        ];

        // aliases map shortcut names to the canonical command
        private static readonly Dictionary<string, string> Aliases =
            new(StringComparer.InvariantCultureIgnoreCase)
        {
            { "ls", "list-account" },
            { "quit", "exit-console" },
            { "help", "show-help" },
            { "cat", "show-account" },
            { "sync", "sync-passwords" }
        };

        // helper to return all tokens that may be tab‑completed (commands + aliases)
        private static List<string> AllCommandsAndAliases()
        {
            var list = new List<string>(Commands);
            foreach (var alias in Aliases.Keys)
            {
                list.Add(alias);
            }
            return list;
        }

        public void Run()
        {
            ShowHelpCommand();
            var consoleReader = new ConsoleReader
            {
                Expand = Expand
            };
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
                    Console.WriteLine("Failed to dispatch command. Reason: {0}", ex.Message);
                }
            }
        }

        private Tuple<List<string>, int> Expand(string cmdline, int cmdpos)
        {
            var ret = new List<string>();
            var result = Shell.Parse(cmdline);
            // start with the full set of command/alias names
            var match = AllCommandsAndAliases();
            var cmp = "";
            int pos = 0;
            if (result.Count > 0)
            {
                cmp = result[0].Input.ToLowerInvariant();
                pos = result[0].StartPosition;
                // if the first token exactly matches a command (not an alias), don't show default completions
                foreach (var c in Commands)
                {
                    if (string.Equals(c, cmp, StringComparison.InvariantCultureIgnoreCase))
                    {
                        match = [];
                        break;
                    }
                }
                // resolve aliases for sub‑command logic
                string cmdForArgs = cmp;
                if (Aliases.TryGetValue(cmp, out var canonical))
                {
                    cmdForArgs = canonical;
                }
                if (cmdForArgs == "show-account" || cmdForArgs == "edit-account" || cmdForArgs == "delete-account" || cmdForArgs == "open-url")
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
                if ((cmdForArgs == "open-repository" || cmdForArgs == "new-repository") && (
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

        private static Answer AskYesNoCancelQuestion(string txt, Answer? dft = null)
        {
            var cr = new ConsoleReader
            {
                Prefix = $"{txt} (y)es / (n)o / (c)ancel: "
            };
            while (true)
            {
                string dftinput = "";
                if (dft.HasValue)
                {
                    dftinput = dft.Value == Answer.Yes ? "y" : (dft.Value == Answer.No ? "n" : "c");
                }
                var input = cr.Read(dftinput).ToLowerInvariant();
                if (input == "y" || input == "yes")
                {
                    return Answer.Yes;
                }
                if (input == "n" || input == "no")
                {
                    return Answer.No;
                }
                if (input == "c" || input == "cancel")
                {
                    return Answer.Cancel;
                }
            }
        }

        private static Answer AskYesNoQuestion(string txt, Answer? dft = null)
        {
            var cr = new ConsoleReader
            {
                Prefix = $"{txt} (y)es / (n)o: "
            };
            while (true)
            {
                string dftinput = "";
                if (dft.HasValue)
                {
                    dftinput = dft.Value == Answer.Yes ? "y" : "n";
                }
                var input = cr.Read(dftinput).ToLowerInvariant();
                if (input == "y" || input == "yes")
                {
                    return Answer.Yes;
                }
                if (input == "n" || input == "no")
                {
                    return Answer.No;
                }
            }
        }

        private bool CheckSaveChanges()
        {
            if (repository?.Changed == true)
            {
                Console.WriteLine("The repository has been changed.");
                var answer = AskYesNoCancelQuestion("Do you want to save the changes?", Answer.Yes);
                if (answer == Answer.Yes)
                {
                    repository.Save(repositoryFileName, keyDirectory, repositoryPassword);
                    return true;
                }
                if (answer == Answer.Cancel)
                {
                    return false;
                }
            }
            return true;
        }

        private Password FindAccount(string name)
        {
            foreach (var pwditem in repository.Passwords)
            {
                if (string.Equals(pwditem.Name, name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return pwditem;
                }
            }
            return null;
        }

        private bool DispatchCommand(ConsoleReader consoleReader)
        {
            var ret = true;
            var parseResult = Shell.Parse(consoleReader.Read());
            if (parseResult.Count > 0 && parseResult[0].InputToken == Shell.Token.ID)
            {
                // map alias to canonical command name if necessary
                var cmd = parseResult[0].Input.ToLowerInvariant();
                if (Aliases.TryGetValue(cmd, out var canonical))
                {
                    cmd = canonical;
                }

                switch (cmd)
                {
                    case "new-repository":
                        NewRepositoryCommand(parseResult);
                        break;
                    case "open-repository":
                        OpenRepositoryCommand(parseResult);
                        break;
                    case "show-repository":
                        ShowRepositoryCommand(parseResult);
                        break;
                    case "edit-repository":
                        EditRepositoryCommand(parseResult);
                        break;
                    case "save-repository":
                        SaveRepositoryCommand(parseResult);
                        break;
                    case "close-repository":
                        CloseRepositoryCommand(parseResult);
                        break;
                    case "change-masterpassword":
                        ChangeMasterPasswordCommand(parseResult);
                        break;
                    case "list-account":
                        ListAccountCommand(parseResult);
                        break;
                    case "add-account":
                        AddAccountCommand(parseResult);
                        break;
                    case "delete-account":
                        DeleteAccountCommand(parseResult);
                        break;
                    case "edit-account":
                        EditAccountCommand(parseResult);
                        break;
                    case "show-account":
                        ShowAccountCommand(parseResult);
                        break;
                    case "open-url":
                        OpenURLCommand(parseResult);
                        break;
                    case "exit-console":
                        if (CheckSaveChanges())
                        {
                            ret = false;
                        }
                        break;
                    case "clear-console":
                        ClearConsoleCommand();
                        break;
                    case "show-help":
                        ShowHelpCommand();
                        break;
                    case "sync-passwords":
                        SyncPasswordsCommand(parseResult);
                        break;
                    case "show-license":
                        ShowLicenseCommand();
                        break;
                    default:
                        Console.WriteLine("Invalid command. Type Show-Help.");
                        break;
                }
            }
            return ret;
        }


        private static void ShowHelpCommand()
        {
            Console.WriteLine("Myna Password Manager Console version 10.0.3");
            Console.WriteLine("Usage: MynaPasswordManagerConsole [password-file]");
            Console.WriteLine("Copyright (c) 2026 Niels Stockfleth. All rights reserved.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  Add-Account                       - Adds an account.");
            Console.WriteLine("  Show-Account <account>            - Displays account information.");
            Console.WriteLine("  Edit-Account <account>            - Edits an account.");
            Console.WriteLine("  Delete-Account <account>          - Deletes an account.");
            Console.WriteLine("  List-Account [<filter>]           - Lists accounts.");
            Console.WriteLine("  Open-URL <account>                - Opens the account's URL in a browser.");
            Console.WriteLine("  New-Repository <file> [<keydir>]  - Creates a new password repository (defaults keys to a 'Keys' subfolder).");
            Console.WriteLine("  Open-Repository <file> [<keydir>] - Opens a password repository (defaults to a 'Keys' subfolder in the repository directory if omitted).");
            Console.WriteLine("  Show-Repository                   - Displays password repository information.");
            Console.WriteLine("  Edit-Repository                   - Edits password repository information.");
            Console.WriteLine("  Close-Repository                  - Closes the password repository.");
            Console.WriteLine("  Save-Repository                   - Saves the password repository.");
            Console.WriteLine("  Change-MasterPassword             - Changes the repository's master password.");
            Console.WriteLine("  Clear-Console                     - Clears the console.");
            Console.WriteLine("  Exit-Console                      - Exits the program.");
            Console.WriteLine("  Show-Help                         - Displays this text.");
            Console.WriteLine("  Sync-Passwords                   - Synchronizes local and cloud password items.");
            Console.WriteLine();
            Console.WriteLine("Aliases:");
            Console.WriteLine("  ls     -> List-Account");
            Console.WriteLine("  quit   -> Exit-Console");
            Console.WriteLine("  help   -> Show-Help");
            Console.WriteLine("  cat    -> Show-Account");
            Console.WriteLine("  Show-License                      - Displays license information.");
        }

        private static void ShowLicenseCommand()
        {
            Console.WriteLine("Myna Password Manager Console");
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

        // called by the console UI itself when the user types the command
        private void OpenRepositoryCommand(List<Shell.ParseResult> result)
        {
            OpenRepositoryInternal(result);
        }

        // reusable logic for opening a repository (exposed also via helper below)
        private void OpenRepositoryInternal(List<Shell.ParseResult> result)
        {
            if (result.Count < 2)
            {
                Console.WriteLine("Missing <file> argument.");
                return;
            }
            if (!File.Exists(result[1].Input))
            {
                Console.WriteLine("Password repository file does not exist.");
                return;
            }
            if (repository != null)
            {
                Console.WriteLine("Repository has not been closed.");
                return;
            }
            repositoryFileName = Path.GetFullPath(result[1].Input);
            if (result.Count > 2)
            {
                keyDirectory = Path.GetFullPath(result[2].Input);
                if (!Directory.Exists(keyDirectory))
                {
                    Console.WriteLine("Key directory does not exist.");
                    return;
                }
            }
            else
            {
                // default to a "Keys" subfolder beside the repository file
                var repoDir = Path.GetDirectoryName(repositoryFileName);
                if (string.IsNullOrEmpty(repoDir))
                {
                    repoDir = Directory.GetCurrentDirectory();
                }
                keyDirectory = Path.Combine(repoDir, "Keys");
                // ensure the directory exists so read/write won't fail
                try
                {
                    Directory.CreateDirectory(keyDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create key directory '{keyDirectory}': {ex.Message}");
                    return;
                }
            }
            var cs = new ConsoleReader
            {
                Prefix = "Master Password: "
            };
            repositoryPassword = cs.ReadSecure();
            try
            {
                repository = PasswordRepository.Read(repositoryFileName, keyDirectory, repositoryPassword, false);
                Console.WriteLine("Repository opened.");
            }
            catch
            {
                Console.WriteLine("Access denied.");
            }
        }

        /// <summary>
        /// Public helper that can be invoked programmatically to open a repository
        /// file without entering the console command manually.  Used by
        /// <see cref="Program.Main"/> to support the optional filename argument.
        /// </summary>
        /// <param name="path">path to the repository file</param>
        public void OpenRepositoryFile(string path)
        {
            // construct parse result equivalent to typing: Open-Repository <path>
            var pr = new List<Shell.ParseResult>
            {
                new Shell.ParseResult(0, 0, Shell.Token.ID, "open-repository"),
                new Shell.ParseResult(0, 0, Shell.Token.STRING, path)
            };
            OpenRepositoryInternal(pr);
        }

        private void ShowRepositoryCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            var changed = repository.Changed ? "yes" : "no";
            Console.WriteLine($"Name: {repository.Name}");
            Console.WriteLine($"Description: {repository.Description}");
            Console.WriteLine($"Accounts: {repository.Passwords.Count}");
            Console.WriteLine($"File: {repositoryFileName}");
            Console.WriteLine($"Key directory: {keyDirectory}");
            Console.WriteLine($"ID: {repository.Id}");
            Console.WriteLine($"Changed: {changed}");
        }

        private void EditRepositoryCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            var cr = new ConsoleReader
            {
                Prefix = "Name: "
            };
            var name = cr.Read(repository.Name);
            cr.Prefix = "Description: ";
            var desc = cr.Read(repository.Description);
            if (AskYesNoQuestion("Do you want to update the repository?") == Answer.Yes)
            {
                repository.Name = name;
                repository.Description = desc;
                Console.WriteLine("Repository updated.");
            }
        }

        private void NewRepositoryCommand(List<Shell.ParseResult> result)
        {
            if (result.Count < 2)
            {
                Console.WriteLine("Missing <file> argument.");
                return;
            }
            if (File.Exists(result[1].Input))
            {
                Console.WriteLine("Password repository file already exists.");
                return;
            }
            if (repository != null)
            {
                Console.WriteLine("Repository has not been closed.");
                return;
            }
            repositoryFileName = Path.GetFullPath(result[1].Input);
            if (result.Count > 2)
            {
                keyDirectory = Path.GetFullPath(result[2].Input);
                if (!Directory.Exists(keyDirectory))
                {
                    Console.WriteLine("Key directory does not exist.");
                    return;
                }
            }
            else
            {
                // default to a "Keys" subfolder beside the repository file
                var repoDir = Path.GetDirectoryName(repositoryFileName);
                if (string.IsNullOrEmpty(repoDir))
                {
                    repoDir = Directory.GetCurrentDirectory();
                }
                keyDirectory = Path.Combine(repoDir, "Keys");
                try
                {
                    Directory.CreateDirectory(keyDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create key directory '{keyDirectory}': {ex.Message}");
                    return;
                }
            }
            var cr = new ConsoleReader
            {
                Prefix = "Name: "
            };
            var name = cr.Read(Path.GetFileNameWithoutExtension(repositoryFileName));
            cr.Prefix = "Description: ";
            var desc = cr.Read();
            cr.Prefix = "Master Password: ";
            repositoryPassword = cr.ReadSecure();
            if (repositoryPassword.Length == 0)
            {
                Console.WriteLine("Aborted.");
                return;
            }
            while (true)
            {
                cr.Prefix = "Confirm Master Password: ";
                var confirm = cr.ReadSecure();
                if (confirm.Length == 0)
                {
                    Console.WriteLine("Aborted.");
                    return;
                }
                if (confirm.IsEqualTo(repositoryPassword))
                {
                    break;
                }
                Console.WriteLine("Passwords do not match.");
            }
            if (AskYesNoQuestion("Do you want to create the repository?") == Answer.Yes)
            {
                repository = new PasswordRepository
                {
                    Name = name,
                    Description = desc
                };
                repository.Save(repositoryFileName, keyDirectory, repositoryPassword);
                Console.WriteLine("Repository created.");
            }
        }

        private void SaveRepositoryCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            if (!repository.Changed)
            {
                Console.WriteLine("Password repository has not been changed.");
                return;
            }
            if (AskYesNoQuestion("Do you want to save the repository?") == Answer.Yes)
            {
                repository.Save(repositoryFileName, keyDirectory, repositoryPassword);
                Console.WriteLine("Repository saved.");
            }
        }

        private void CloseRepositoryCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            if (!CheckSaveChanges())
            {
                Console.WriteLine("Aborted.");
                return;
            }
            repository = null;
            repositoryPassword.Clear();
            Console.WriteLine("Repository closed.");
        }

        private void ChangeMasterPasswordCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            if (!CheckSaveChanges())
            {
                Console.WriteLine("Aborted.");
                return;
            }
            var cr = new ConsoleReader
            {
                Prefix = "Current Master Password: "
            };
            var check = cr.ReadSecure();
            if (!check.IsEqualTo(repositoryPassword))
            {
                Console.WriteLine("Access denied.");
                return;
            }
            cr.Prefix = "New Master Password: ";
            var newrepositoryPassword = cr.ReadSecure();
            if (newrepositoryPassword.Length == 0)
            {
                Console.WriteLine("Aborted.");
                return;
            }
            while (true)
            {
                cr.Prefix = "Confirm New Master Password: ";
                var confirm = cr.ReadSecure();
                if (confirm.Length == 0)
                {
                    Console.WriteLine("Aborted.");
                    return;
                }
                if (confirm.IsEqualTo(newrepositoryPassword))
                {
                    break;
                }
                Console.WriteLine("Passwords do not match.");
            }
            if (AskYesNoQuestion("Do you want to change the master password?") == Answer.Yes)
            {
                repository.ChangeMasterPassword(repositoryFileName, keyDirectory, newrepositoryPassword);
                repositoryPassword = newrepositoryPassword;
                Console.WriteLine("Master password changed.");
            }
        }

        // synchronize local password entries with the cloud copy
        private void SyncPasswordsCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }

            try
            {
                // initialize REST client with cloud hostname
                var locale = "en-US";
                var host = "www.stockfleth.eu";
                CloudExport.CloudExport.Init(host, locale).GetAwaiter().GetResult();

                // authenticate against the cloud; interactive prompts will be shown
                var token = CloudExport.CloudExport.AuthenticateAsync(null, null, null, locale).GetAwaiter().GetResult();
                var userModel = CloudExport.CloudExport.GetUserModel(token).GetAwaiter().GetResult();
                var salt = userModel.passwordManagerSalt;
                // encryption key for cloud items
                var key = CloudExport.ConsoleUtils.ReadSecret("LABEL_KEY");

                var cloudItems = CloudExport.CloudExport.FetchPasswordItemsAsync(token, key, salt).GetAwaiter().GetResult();

                // determine differences
                var local = repository.Passwords;
                var missingLocal = cloudItems.Where(ci => !local.Any(lp => string.Equals(lp.Name, ci.Name, StringComparison.InvariantCultureIgnoreCase))).ToList();
                var missingCloud = local.Where(lp => cloudItems.All(ci => !string.Equals(ci.Name, lp.Name, StringComparison.InvariantCultureIgnoreCase))).ToList();

                var diffs = new List<(Password local, CloudExport.Services.PasswordItem cloud)>();
                foreach (var lp in local)
                {
                    var ci = cloudItems.FirstOrDefault(c => string.Equals(c.Name, lp.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (ci != null)
                    {
                        if (ci.Url != lp.Url || ci.Login != lp.Login || ci.Password != lp.SecurePassword.GetAsString() || ci.Description != lp.Description)
                        {
                            diffs.Add((lp, ci));
                        }
                    }
                }

                if (missingLocal.Count > 0)
                {
                    Console.WriteLine($"Adding {missingLocal.Count} items from cloud to local repository:");
                    foreach (var ci in missingLocal)
                    {
                        var pwd = new Password
                        {
                            Name = ci.Name,
                            Url = ci.Url,
                            Login = ci.Login,
                            Description = ci.Description
                        };
                        var ss = new SecureString();
                        foreach (var ch in ci.Password)
                        {
                            ss.AppendChar(ch);
                        }
                        pwd.SecurePassword = ss;
                        repository.Add(pwd);
                        Console.WriteLine($"  {ci.Name}");
                    }
                    // save after adding
                    repository.Save(repositoryFileName, keyDirectory, repositoryPassword);
                }

                if (missingCloud.Count > 0)
                {
                    Console.WriteLine($"Adding {missingCloud.Count} items from local to cloud:");
                    foreach (var lp in missingCloud)
                    {
                        var ci = new CloudExport.Services.PasswordItem
                        {
                            Name = lp.Name,
                            Url = lp.Url,
                            Login = lp.Login,
                            Description = lp.Description,
                            Password = lp.SecurePassword.GetAsString()
                        };
                        cloudItems.Add(ci);
                        Console.WriteLine($"  {lp.Name}");
                    }
                    CloudExport.CloudExport.SavePasswordItemsAsync(token, key, salt, cloudItems).GetAwaiter().GetResult();
                }

                if (diffs.Count > 0)
                {
                    Console.WriteLine("The following items differ between local and cloud (no changes made):");
                    foreach (var (lp, ci) in diffs)
                    {
                        Console.WriteLine($"  {lp.Name}");
                    }
                }

                if (!missingLocal.Any() && !missingCloud.Any() && !diffs.Any())
                {
                    Console.WriteLine("Local repository is already in sync with cloud.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Synchronization failed: {ex.Message}");
            }
        }

        private void ListAccountCommand(List<Shell.ParseResult> result)
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
                    // display bare name, no quoting required
                    Console.WriteLine(password.Name);
                }
            }
        }

        private void OpenURLCommand(List<Shell.ParseResult> result)
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
            var pwditem = FindAccount(result[1].Input);
            if (pwditem != null)
            {
                if (!string.IsNullOrEmpty(pwditem.Url))
                {
                    Shell.OpenURL(pwditem.Url);
                }
                else
                {
                    Console.WriteLine("No URL has been configured for the account.");
                }
            }
        }

        private void AddAccountCommand(List<Shell.ParseResult> result)
        {
            if (repository == null)
            {
                Console.WriteLine("Password repository has not been opened.");
                return;
            }
            var cr = new ConsoleReader
            {
                Prefix = "Name: "
            };
            string name;
            while (true)
            {
                name = cr.Read();
                if (FindAccount(name) == null)
                {
                    break;
                }
                Console.WriteLine("The account already exists.");
            }
            cr.Prefix = "URL: ";
            var url = cr.Read();
            cr.Prefix = "Login: ";
            var login = cr.Read();
            cr.Prefix = "Password: ";
            var pwd = cr.ReadSecure();
            cr.Prefix = "Description: ";
            var desc = cr.Read();
            if (AskYesNoQuestion("Do you want to add the account?") == Answer.Yes)
            {
                var pwditem = new Password
                {
                    Name = name,
                    Description = desc,
                    Url = url,
                    Login = login,
                    SecurePassword = pwd
                };
                repository.Add(pwditem);
                Console.WriteLine("Account added.");
            }
        }

        private void EditAccountCommand(List<Shell.ParseResult> result)
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
            var pwditem = FindAccount(result[1].Input);
            if (pwditem != null)
            {
                var cr = new ConsoleReader
                {
                    Prefix = "Name: "
                };
                string newname;
                while (true)
                {
                    newname = cr.Read(pwditem.Name);
                    if (newname == pwditem.Name || FindAccount(newname) == null)
                    {
                        break;
                    }
                    Console.WriteLine("The account already exists.");
                }
                cr.Prefix = "URL: ";
                var url = cr.Read(pwditem.Url);
                cr.Prefix = "Login: ";
                var login = cr.Read(pwditem.Login);
                cr.Prefix = "Password: ";
                var pwd = cr.ReadSecure();
                cr.Prefix = "Description: ";
                var desc = cr.Read(pwditem.Description);
                if (AskYesNoQuestion("Do you want to update the account?") == Answer.Yes)
                {
                    if (pwd.Length > 0)
                    {
                        pwditem.SecurePassword = pwd;
                    }
                    if (pwditem.Name != newname)
                    {
                        pwditem.Name = newname;
                    }
                    pwditem.Url = url;
                    pwditem.Login = login;
                    pwditem.Description = desc;
                    repository.Update(pwditem);
                    Console.WriteLine("Account updated.");
                }
            }
        }

        private void ShowAccountCommand(List<Shell.ParseResult> result)
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
            var pwditem = FindAccount(result[1].Input);
            if (pwditem != null)
            {
                Console.WriteLine($"Name: {pwditem.Name}");
                Console.WriteLine($"URL: {pwditem.Url}");
                Console.WriteLine($"Login: {pwditem.Login}");
                Console.WriteLine($"Password: {pwditem.SecurePassword.GetAsString()}");
                Console.WriteLine($"Description: {pwditem.Description}");
            }
        }

        private void DeleteAccountCommand(List<Shell.ParseResult> result)
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
            var pwditem = FindAccount(result[1].Input);
            if (pwditem != null)
            {
                if (AskYesNoQuestion("Do you want to remove the account?") == Answer.Yes)
                {
                    repository.Remove(pwditem);
                    Console.WriteLine("Account deleted.");
                }
            }
        }
    }
}