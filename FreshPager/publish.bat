@echo off

dotnet restore
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false --no-restore
dotnet publish -c Release -r linux-arm -p:PublishSingleFile=true --self-contained false --no-restore