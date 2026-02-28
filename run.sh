#!/bin/sh
# launcher for the published console application

# pass any provided arguments through to the program

dotnet ~/src/MynaPasswordManagerConsole/bin/Release/net10.0/publish/MynaPasswordManagerConsole.dll "$@"
