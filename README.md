# Myna Password Manager Console

A lightweight console application written in .NET that lets you create, open and
manipulate encrypted password repositories. The program stores credentials in
an XML file, with each entry protected by a separate key file kept in a
`Keys` subdirectory. It provides command history, tab‑completion, and a small
set of intuitive commands and aliases for quick navigation.

---

## Features

- **Create, open, save, and close** password repositories
- **Add, edit, delete and list** account entries
- **Secure master password** with per-account keys and AES encryption
- **Command aliases** (`ls`, `cat`, `clear`, `help`, `go`, `quit`, `sync`) and tab-completion
- **Optional CLI argument** to open a repository immediately
- Simple **import/export** support via copy/paste (not shown here)


## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) (the project targets `net10.0`)
- macOS, Linux or Windows with a terminal


## Building

```bash
cd /path/to/MynaPasswordManagerConsole
dotnet build
```

The build output lands under `bin/Debug/net10.0` (or `bin/Release/...` when published).


## Running

### Interactive mode

Start the console and type commands as needed:

```bash
dotnet run --project MynaPasswordManagerConsole.csproj
```

or after publishing:

```bash
dotnet bin/Release/net10.0/publish/MynaPasswordManagerConsole.dll
```

### Open a repository on startup

Supply an optional password file name as the first argument; the UI will
prompt for the master password and open it automatically.

```bash
# launch and immediately open "myrepo.myna"
./run.sh myrepo.myna
```

### Example session

```
$ ./run.sh
Usage: MynaPasswordManagerConsole [password-file]
# create new repository
New-Repository passwords.myna
Name: Personal
Description: Logins
Master Password: ******
Confirm Master Password: ******
Repository created.
# add an account
Add-Account
Name: GitHub
URL: https://github.com
Login: nyls
Password: ******
# list entries (alias)
ls
GitHub
# show details (alias)
cat GitHub
Name: GitHub
URL: https://github.com
Login: nyls
Password: ******
# open URL (alias)
go GitHub
# clear screen (alias)
clear
# sync repository (alias)
sync
# exit (alias)
quit
```


## Commands & aliases

The console accepts full commands and several short aliases:

| Command            | Alias  | Description                     |
|--------------------|--------|---------------------------------|
| `List-Account`     | `ls`   | Show all account names          |
| `Add-Account`      |        | Create a new account entry      |
| `Edit-Account`     |        | Modify an existing entry        |
| `Delete-Account`   |        | Remove an entry                 |
| `Show-Account`     | `cat`  | Display account details         |
| `Open-URL`         | `go`   | Open the account's URL/browser  |
| `New-Repository`   |        | Create a new repository file    |
| `Open-Repository`  |        | Open an existing repository     |
| `Show-Repository`  |        | Show repository metadata        |
| `Save-Repository`  |        | Persist changes                 |
| `Close-Repository` |        | Close current repository        |
| `Change-MasterPassword` |   | Change master password          |
| `Sync-Passwords`   | `sync`| Synchronize with cloud copy     |
| `Clear-Console`    | `clear`| Clear the screen               |
| `Show-Help`        | `help` | Display help text               |
| `Show-License`     |        | Show GPL license text           |
| `Exit-Console`     | `quit` | Exit application                |

Commands support tab completion for names and file paths (type part of
name then hit `Tab`).


## Notes

- Keys are stored in a `Keys/` directory alongside the repository file. If
  the directory does not exist it will be created automatically.
- The tool now supports optional cloud synchronization using the
  `Sync-Passwords` command; the command will connect with the cloud host
  `www.stockfleth.eu`. A valid user account is required. Local-only
  usage is still fully supported.

---

Feel free to fork, submit issues or patches! See `LICENSE` for licensing info.
