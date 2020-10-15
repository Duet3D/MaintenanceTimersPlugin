@echo off

cd src
del ../plugin/bin/*
dotnet publish -r linux-arm -o ../plugin/bin/