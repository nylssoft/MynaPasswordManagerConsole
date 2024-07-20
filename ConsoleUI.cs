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
using System.Security;

namespace PasswordManagerConsole
{
    public class ConsoleUI
    {
        private enum Answer { Yes, No, Cancel };

        private PasswordRepository repository = null;
        private string repositoryFileName;
        private string keyDirectory;
        private SecureString repositoryPassword;

        private static List<string> Commands = new List<string> {
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
            "Show-License"
        };

        public void Run()
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
                    Console.WriteLine("Failed to dispatch command. Reason: {0}", ex.Message);
                }
            }
        }

        private Tuple<List<string>, int> Expand(string cmdline, int cmdpos)
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
                if (cmp == "show-account" || cmp == "edit-account" || cmp == "delete-account" || cmp == "open-url")
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
                if ((cmp == "open-repository" || cmp == "new-repository") && (
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

        private Answer AskYesNoCancelQuestion(string txt, Answer? dft = null)
        {
            var cr = new ConsoleReader();
            cr.Prefix = $"{txt} (y)es / (n)o / (c)ancel: ";
            while (true)
            {
                string dftinput = "";
                if (dft.HasValue)
                {
                    dftinput = dft.Value == Answer.Yes ? "y" : (dft.Value == Answer.No ? "n" : "c");
                }
                var input = cr.Read(dftinput).ToLowerInvariant();
                if (input=="y" || input=="yes")
                {
                    return Answer.Yes;
                }
                if (input=="n" || input=="no")
                {
                    return Answer.No;
                }
                if (input=="c" || input == "cancel")
                {
                    return Answer.Cancel;
                }
            }
        }

        private Answer AskYesNoQuestion(string txt, Answer? dft = null)
        {
            var cr = new ConsoleReader();
            cr.Prefix = $"{txt} (y)es / (n)o: ";
            while (true)
            {
                string dftinput = "";
                if (dft.HasValue)
                {
                    dftinput = dft.Value == Answer.Yes ? "y" : "n";
                }
                var input = cr.Read(dftinput).ToLowerInvariant();
                if (input=="y" || input=="yes")
                {
                    return Answer.Yes;
                }
                if (input=="n" || input=="no")
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
                switch (parseResult[0].Input.ToLowerInvariant())
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


        private void ShowHelpCommand()
        {
            Console.WriteLine("Myna Password Manager Console version 8.0.1");
            Console.WriteLine("Copyright (c) 2024 Niels Stockfleth. All rights reserved.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  Add-Account                       - Adds an account.");
            Console.WriteLine("  Show-Account <account>            - Displays account information.");
            Console.WriteLine("  Edit-Account <account>            - Edits an account.");
            Console.WriteLine("  Delete-Account <account>          - Deletes an account.");
            Console.WriteLine("  List-Account [<filter>]           - Lists accounts.");
            Console.WriteLine("  Open-URL <account>                - Opens the account's URL in a browser.");
            Console.WriteLine("  New-Repository <file> [<keydir>]  - Creates a new password repository.");
            Console.WriteLine("  Open-Repository <file> [<keydir>] - Opens a password repository.");
            Console.WriteLine("  Show-Repository                   - Displays password repository information.");
            Console.WriteLine("  Edit-Repository                   - Edits password repository information.");
            Console.WriteLine("  Close-Repository                  - Closes the password repository.");
            Console.WriteLine("  Save-Repository                   - Saves the password repository.");
            Console.WriteLine("  Change-MasterPassword             - Changes the repository's master password.");
            Console.WriteLine("  Clear-Console                     - Clears the console.");
            Console.WriteLine("  Exit-Console                      - Exits the program.");
            Console.WriteLine("  Show-Help                         - Displays this text.");
            Console.WriteLine("  Show-License                      - Displays license information.");
        }

        private void ShowLicenseCommand()
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

        private void ClearConsoleCommand()
        {
            Console.Clear();
        }

        private void OpenRepositoryCommand(List<Shell.ParseResult> result)
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
                keyDirectory = Path.GetDirectoryName(repositoryFileName);
            }
            var cs = new ConsoleReader();
            cs.Prefix = "Master Password: ";
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
            var cr = new ConsoleReader();
            cr.Prefix = "Name: ";
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
                keyDirectory = Path.GetDirectoryName(repositoryFileName);
            }
            var cr = new ConsoleReader();
            cr.Prefix = "Name: ";
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
                repository = new PasswordRepository();
                repository.Name = name;
                repository.Description = desc;
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
            var cr = new ConsoleReader();
            cr.Prefix = "Current Master Password: ";
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
            var cr = new ConsoleReader();
            cr.Prefix = "Name: ";
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
                var cr = new ConsoleReader();
                cr.Prefix = "Name: ";
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