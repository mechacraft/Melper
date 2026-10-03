@echo off
rem Double-click: build and deploy all mods. Arguments pass through, e.g. "deploy.bat -Run".
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
pause
