@echo off
title DFC to SNAP V2 Migration Tool
rem Double-click to start the interactive menu. You can also drag the customer's data folder onto this file.
if not exist "%~dp0dist\dfcmigrate.exe" (
    echo  ERROR: dist\dfcmigrate.exe not found next to this file. Build it first - see README.md.
    pause
    exit /b 1
)
"%~dp0dist\dfcmigrate.exe" %*
