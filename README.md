# Simple Patch Tool

### THIS PROJECT IS NO LONGER MAINTAINED.

---

![screenshot](Images/launcher-winforms.png)

SimplePatchTool is a general-purpose patcher library for **standalone** applications.

**[Support the Developer ☕](https://yasirkula.itch.io/unity3d)**

## FEATURES

- written completely in **C#**
- supports **repair patching** as well as **binary diff patching**
- gives you complete control over where the patch files are stored (you can even host your files on Google Drive™)
- supports self patching (e.g. launcher patching itself)
- does not request admin permissions unless necessary
- supports encrypting/signing important patch files as an additional layer of security
- compatible with **.NET Standard 2.0** (the *optional* XML signing module requires [additional NuGet package(s)](https://www.nuget.org/packages/System.Security.Cryptography.Xml/)) and **Mono 2.0** (according to official [.NET Portability Analyzer](https://docs.microsoft.com/en-us/dotnet/standard/analyzers/portability-analyzer))

Currently, this library is only tested on a 64-bit Windows 10 installment. Please note that SimplePatchTool is not yet battle tested thoroughly, so you may encounter unknown issues while integrating it into your projects. Don't hesitate to open an Issue when you encounter one!

**Unity 3D** port available at: https://github.com/yasirkula/UnitySimplePatchTool

## LICENSE

SimplePatchTool is licensed under the [MIT License](LICENSE); however, it uses external libraries that are governed by the licenses indicated below:

- LZMA SDK - [Public Domain](https://www.7-zip.org/sdk.html)
- Octodiff - [Apache License, Version 2.0](https://github.com/OctopusDeploy/Octodiff/blob/master/LICENSE.txt)
- SharpZipLib - [MIT License](https://github.com/icsharpcode/SharpZipLib/blob/master/LICENSE.txt)

## DOCUMENTATION

Wiki available at: https://github.com/yasirkula/SimplePatchTool/wiki

## ROADMAP

- [x] add another patch method that packs all files in the version into a single compressed archive
- [x] calculate percentage of the overall progress
- [ ] calculate the estimated remaining time
