@echo off
set path=%PATH%;C:\Program Files\7-Zip;

rd .\Publish /q /s

md Publish

md Publish\CloudflareDNSUpdate
dotnet publish .\CloudflareDNSUpdate -c Release -o publish\CloudflareDNSUpdate\CloudflareDNSUpdate
del publish\CloudflareDNSUpdate\CloudflareDNSUpdate\*.pdb
7z.exe a -tzip -r -mx9 ".\Publish\CloudflareDNSUpdate.zip" ".\Publish\CloudflareDNSUpdate\*.*"
rd .\Publish\CloudflareDNSUpdate /q /s

pause
