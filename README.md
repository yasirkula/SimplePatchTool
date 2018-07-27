# Simple Patch Tool

SimplePatchTool is a general-purpose patcher library for **standalone** applications.

## A. FEATURES

- written completely in **C#**
- supports **repair patching** as well as **binary diff patching**
- gives you complete control over where you store the patch files at (you can even host your files on Google Drive™)
- supports self patching (e.g. launcher patching itself)
- does not request admin permissions unless necessary
- supports encrypting/signing important patch files as an additional layer of security
- compatible with **.NET Standard 2.0** (the *optional* XML signing module requires [additional NuGet package(s)](https://www.nuget.org/packages/System.Security.Cryptography.Xml/)) and **Mono 2.0** (according to official [.NET Portability Analyzer](https://docs.microsoft.com/en-us/dotnet/standard/analyzers/portability-analyzer))

Currently, this library is only tested on a 64-bit Windows 10 installment. Please note that SimplePatchTool is not yet battle tested thoroughly, so you may encounter unknown issues while integrating it into your projects. Don't hesitate to open an Issue when you encounter one!

**Unity 3D** port available at: https://github.com/yasirkula/UnitySimplePatchTool

## B. LICENSE

SimplePatchTool is licensed under the [MIT License](LICENSE); however, it uses external libraries that are governed by the licenses indicated below:

- LZMA SDK - [Public Domain](https://www.7-zip.org/sdk.html)
- Octodiff - [Apache License, Version 2.0](https://github.com/OctopusDeploy/Octodiff/blob/master/LICENSE.txt)
- SharpZipLib - [MIT License](https://github.com/icsharpcode/SharpZipLib/blob/master/LICENSE.txt)

## C. GLOSSARY

- **Repair patch:** a patch method that downloads the updated files from the server and replaces local files with them
- **Incremental patch:** a patch method that downloads *binary diff files* of the updated files from the server and applies these diffs to the local files. Incremental patches can only be applied to a specific version of the app, i.e. an incremental patch that patches the app from version 1.1 to 1.2 can not be applied if installed app's version is not 1.1. Similarly, if installed app's version is 1.0 and there are two incremental patches (1.0->1.1 and 1.1->1.2), these patches are applied consecutively to update the app to the 1.2 version
- **Binary diff file:** stores the changes between two versions of the same file. It is usually more efficient to download a small diff file rather than downloading the whole file
- **Self patching:** when enabled, patcher does not modify the files in the application directory but rather apply the patches to the *cache directory*. After all patches are applied there, application terminates itself and launches the *self patcher*. Self patching is the way to go when e.g. you want the launcher to patch itself, because launchers can't patch themselves in conventional ways while they are still running
- **Self patcher:** a tiny executable that is responsible for moving the self-patched files from the *cache directory* to the application directory and deleting any *obsolete files* afterwards
- **Obsolete file:** files in the application directory that are not part of the up-to-date version of the app. These files are deleted after the application is patched
- **Cache directory:** a path that holds the temporary patch files while applying a patch. Its contents are automatically cleaned after a successful patch. It is located at `{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\SimplePatchToolDls\{PROJECT_NAME}`
- **VersionInfo:** an *xml* file that stores all the necessary information to update an app
- **PatchInfo:** an *xml* file that stores detailed information about an incremental patch

**NOTE:** it is possible for an application to self patch itself, which eliminates the need for a launcher; but it is not recommended for large applications because if user terminates the self patcher before it updates all the files in the application directory (which may take some time for large applications), then the application may become corrupt.

## D. QUICK START

This part will guide you through using the console app to perform essential patch operations. Even if you plan to use [Scripting API](#e-scripting-api) for these operations, it's highly recommended that you read this section as it contains useful information about SimplePatchTool in general.

To generate the console app, open **SimplePatchTool.sln** and rebuild the **SimplePatchToolConsoleApp** project. If you are targeting *.NET Standard 2.0* or *.NET Core 2.0*, you'll need to add [this NuGet package](https://www.nuget.org/packages/System.Security.Cryptography.Xml/) as reference to the **SimplePatchToolSecurity** project.

Console app uses two kinds of arguments: string arguments and flags. String arguments should be entered as `-argName="arg value"`, whereas flags should be entered directly as `-argName`.

### D.1. Creating Patches

Use the `Patcher create` command. It takes the following arguments:

- **root:** path of the up-to-date (latest) version of your application
- **out:** patch files will be generated here
- **name:** a unique name for your SimplePatchTool project. This name is used in several key locations, so give it a meaningful name with low collision chance and, unless absolutely necessary, **do not** change it after releasing the first version of your app. Name can only contain English letters and numbers
- **version:** version code of the latest version of your app, e.g. `1.0`, `0.4.11.3` and so on
- **prevRoot:** (*optional*) path of the previous version of your application. Providing a *prevRoot* will create an incremental patch. If this is the first release of your app, don't use this argument
- **ignoredPaths:** (*optional*) path of a text file that stores paths of files/directories relative to the root path that SimplePatchTool should ignore. These files/directories are not included in the patch but they are also not counted as obsolete files, so they are not deleted while patching the app. Ignored paths are evaluated as regular expressions: currently only **\*** (zero or more characters) and **?** (zero or one character) special characters are supported. *ignoredPaths* file should store one ignored path per line
- **dontCreateRepairPatch:** (*optional*)(**_flag_**) as the name suggests, instructs SimplePatchTool to not generate a repair patch. Might be useful when e.g. you have created a patch from version 1.2 to 1.3 and now you want to create only an incremental patch that patches directly from version 1.1 to 1.3 (and not wait for SimplePatchTool to regenerate the same repair files)
- **silent:** (*optional*)(**_flag_**) progress will not be logged to the console

Example usage: `Patcher create -root="C:\App v1.1" -prevRoot="C:\App v1.0" -version="1.1" -out="C:\PatchFiles" -name="MyProjectName"`

After you create a patch, a file named `{name}_vers.sptv` will automatically be created in *root* directory. This file simply stores the *version* of the application. This file is ignored by SimplePatchTool implicitly, so you don't have to add it to the ignored paths list.

### D.2. Updating Patches

Currently, after you create a patch, generated *VersionInfo.info* file will only have one `<IncrementalPatch>` in it (or zero, if you didn't provide a *prevRoot*) and all the download links will be blank. Here are the things you should do after creating a patch:

- if you had a previous VersionInfo file, copy all `<IncrementalPatch>`es from it and paste them to the newer VersionInfo
- for download links, you have two options:
  - enter a `<BaseDownloadURL>` (e.g. `http://myserver.com/dl?`) and when SimplePatchTool attempts to download a file (e.g. *ABC.xy*), it will be downloaded from `http://myserver.com/dl?ABC.xy`. In other words, filename will be appended to the BaseDownloadURL to form a download url. For this approach, all you need to do is to move all the patch files except VersionInfo.info into the *RepairFiles* subdirectory and copy that folder to your server. Then put the url of that directory into the BaseDownloadURL (make sure that your app has access to that directory)
  - enter custom `<DownloadURL>`s and/or `InfoURL`s for the VersionItem's (repair files) and/or IncrementalPatch'es. If you want, you can provide custom download urls for only a selection of files, and the rest will be downloaded from the BaseDownloadURL
- (*optional*) enter a `<MaintenanceCheckURL>` and store a text file at that url that will be used to query whether or not servers are currently under maintenance. This text file has a very simple syntax: if its first character is a **1**, it means that servers are currently under maintenance. SimplePatchTool won't apply patches while servers are under maintenance. Second character of the text file can be used to provide additional information to the [Scripting API](#e4-applying-patches): if is not a **1**, it means that user can continue using the application while the servers are under maintenance. Otherwise, user is not allowed to launch the app until servers become functional again

Sometimes, it may not be possible to make use of `<BaseDownloadURL>` in your projects. For example, if you host your files on Google Drive™, each download link will contain a unique id in it, which renders BaseDownloadURL useless. In such cases, if your project contains many files, it can become frustrating to have to manually enter all the *DownloadLink*'s for the VersionItem's by hand. Luckily, you can automate this process via the `Patcher update_links` command:

- **versionInfoPath:** path of the *VersionInfo.info* file
- **linksPath:** path of a text file that stores the download links of the files in the patch in the following format (one file per line): `{File relative path} {File download url}`
- **silent:** (*optional*)(**_flag_**) progress will not be logged to the console

This command automatically updates the download links of the repair files in the VersionInfo with the links provided in the *linksPath* file. On your own servers, you can write a simple script to generate that file. If you plan to host your files on Google Drive™, you can use [this extension](https://github.com/yasirkula/DownloadLinkGeneratorForGoogleDrive) (which I wrote specifically for SimplePatchTool). For Dropbox, you can use [this extension](https://github.com/yasirkula/DownloadLinkGeneratorForDropbox), but please note that SimplePatchTool is not tested with Dropbox.

### D.3. Signing/Verifying Patches (Optional)

As an additional layer of security against man-in-the-middle attacks, you can sign the VersionInfo and PatchInfo files. Then, while patching your app, you can verify their signatures to make sure that they are genuine. This will ensure that hashes and filesizes stored in these files are not tampered with; which, in turn, makes it possible to detect whether or not downloaded patch files are genuine.

A private RSA key is used to sign xml files and a public RSA key is used to verify the signature. Public key is usually embedded into the application but the private key must be stored in a safe location. If private key is compromised, then we can no longer assume that the xml files are signed by a trusted party.

If you haven't generated an RSA key pair already, you can do so via the `Patcher generate_rsa_key_pair` command:

- **private:** the filepath that the private RSA key will be generated at
- **public:** the filepath that the public RSA key will be generated at

Afterwards, you can distribute the public key with your application however you want (you can e.g. copy&paste its contents to a string constant). But make sure to store your private key in a safe location.

To sign the `VersionInfo.info` file or a `{version1}__{version2}.info` file (i.e. PatchInfo file), use the `Patcher sign_xml` command:

- **xml:** path of the xml file to sign
- **key:** path of the **private** RSA key

To test whether or not the file is signed successfully, use the `Patcher verify_xml` command:

- **xml:** path of the signed xml file
- **key:** path of the **public** RSA key

**IMPORTANT:** if you change the contents of an xml file after signing it, you have to sign it again. Otherwise, the signature will no longer be valid.

### D.4. Applying Patches

To apply a patch to a directory, use the `Patcher apply` command:

- **root:** path of an older version of your application that is to be updated
- **versionURL:** url of the VersionInfo
- **dontUseIncrementalPatch:** (*optional*)(**_flag_**) SimplePatchTool will not apply incremental patches
- **dontUseRepair:** (*optional*)(**_flag_**) SimplePatchTool will not apply repair patch
- **verifyFiles:** (*optional*)(**_flag_**) SimplePatchTool will check if all necessary files exist on the server and then verify that their sizes match with the VersionInfo before downloading them. For this to work, however, a *HEAD* request to the file's download url should return response code 200 (*OK*) and filesize should be stored in *Content-Length* header
- **silent:** (*optional*)(**_flag_**) progress will not be logged to the console
- **versionInfoKey:** (*optional*) path of the **public** RSA key that will be used to verify the VersionInfo
- **patchInfoKey:** (*optional*) path of the **public** RSA key that will be used to verify PatchInfo's

Currently, it is not possible to test self patching from the console app.

To quickly test a patch without uploading the files to a server, you can use the **file://** url schema. Simply move all the patch files (except *VersionInfo.info*) into the *RepairFiles* directory and change the *BaseDownloadURL* of the VersionInfo like this: `<BaseDownloadURL>file://C:\path\to\patch\files\RepairFiles\</BaseDownloadURL>` (notice the path separator at the end). Then, use the following *versionURL*: `-versionURL="file://C:\path\to\patch\files\VersionInfo.info"`

## E. SCRIPTING API

**SimplePatchTool.sln** consists of 4 projects:

- **SimplePatchToolCore:** the main module, all the core logic (e.g. creating/applying patches, localization) is implemented here
- **SimplePatchToolSecurity**: contains functions to sign/verify XML files and generate *RSA* key pair. This module requires [additional NuGet package(s)](https://www.nuget.org/packages/System.Security.Cryptography.Xml/) for *.NET Standard 2.0* compatibility
- **SimplePatchToolSelfPatcher**: an example implementation of a console-based self patcher executable. It can be used when you want to self patch your launcher/app but don't want to create a custom UI for the self patcher
- **SimplePatchToolConsoleApp**: console app that is used in the [QUICK START](#d-quick-start) section. It uses *SimplePatchToolCore* and *SimplePatchToolSecurity* modules

### E.1. Creating Patches

**Module:** SimplePatchToolCore

`public PatchCreator( string rootPath, string outputPath, string projectName, VersionCode version )`: creates a new PatchCreator instance. Its parameters correspond to the *root*, *out*, *name* and *version* arguments mentioned in the [QUICK START](#d1-creating-patches) section. *VersionCode* supports implicit casting from *string*

`PatchCreator LoadIgnoredPathsFromFile( string pathToIgnoredPathsList )`: corresponds to the *ignoredPaths* argument mentioned in the [QUICK START](#d1-creating-patches) section

`PatchCreator AddIgnoredPath( string ignoredPath )`: PatchCreator ignores the specified path

`PatchCreator AddIgnoredPaths( IEnumerable<string> ignoredPaths )`: PatchCreator ignores the specified paths

`PatchCreator CreateRepairPatch( bool value )`: sets whether or not a repair patch should be generated

`PatchCreator CreateIncrementalPatch( bool value, string previousVersionRoot = null )`: sets whether or not an incremental patch should be generated. If **value** is equal to *true*, a **previousVersionRoot** must be provided (which corresponds to the *prevRoot* argument mentioned in the [QUICK START](#d1-creating-patches) section)

`PatchCreator SilentMode( bool silent )`: sets whether or not PatchCreator should log anything

`bool Run()`: starts creating the patch asynchronously in a separate thread. This function will return *false*, if PatchCreator is already running

`string FetchLog()`: fetches the next log that PatchCreator has generated. Returns *null*, if there is no log in the queue

`void Cancel()`: cancels the operation

`bool IsRunning { get; }`: returns *true* if PatchCreator is currently running

`PatchResult Result { get; }`: returns *PatchResult.Success* if patch is created successfully, *PatchResult.Failed* otherwise. Its value should be checked after *IsRunning* returns *false*

For example code, see the [SimplePatchToolConsoleApp.Program.CreatePatch](SimplePatchToolConsoleApp/Program.cs) function.

### E.2. Updating Patches

**Module:** SimplePatchToolCore

`public PatchUpdater( string versionInfoPath, LogEvent logger = null )`: creates a new PatchUpdater instance. Path of the *VersionInfo.info* file should be passed to the **versionInfoPath** parameter. PatchUpdater works synchronously and thus, instead of *SilentMode* and *FetchLog* functions, it has a **logger** parameter which has the signature `delegate void LogEvent( string log )`. If a function is provided here, this function will be called for each log. Otherwise, PatchUpdater will work silently

`bool UpdateDownloadLinks( string downloadLinksPath )`: corresponds to the *Patcher update_links* command mentioned in the [QUICK START](#d2-updating-patches) section. Returns *false*, if **downloadLinksPath** doesn't point to an existing text file

`VersionInfo VersionInfo { get; }`: returns the VersionInfo that this PatchUpdater is modifying. Feel free to make any changes to this VersionInfo object

`void SaveChanges()`: updates the *VersionInfo.info* file. Call this function after executing the *UpdateDownloadLinks* function or changing the properties of *VersionInfo*

For example code, see the [SimplePatchToolConsoleApp.Program.UpdateLinks](SimplePatchToolConsoleApp/Program.cs) function.

### E.3. Signing/Verifying Patches (Optional)

**Module:** SimplePatchToolSecurity

`static void SecurityUtils.CreateRSAKeyPair( out string publicKey, out string privateKey )`: creates public and private RSA keys and returns them as raw strings (i.e. it doesn't save the keys to text files)

`static void XMLSigner.SignXMLFile( string xmlPath, string rsaPrivateKey )`: signs an xml file with the provided private RSA key. Note that **rsaPrivateKey** should hold the contents of the private key as a raw string (i.e. don't pass the path of the key here)

`static bool XMLSigner.VerifyXMLFile( string xmlPath, string rsaPublicKey )`: verifies the signature of an xml file with the provided public RSA key (it must be in raw string format). Returns *false*, if the xml file is not genuine

`static bool XMLSigner.VerifyXMLContents( string xml, string rsaPublicKey )`: verifies the signature of a raw xml string with the provided public RSA key (in raw string format). To verify VersionInfo and/or PatchInfo files while actually patching your app, you have to introduce this function to SimplePatchTool (see *UseVersionInfoVerifier* and *UsePatchInfoVerifier* functions below)

For example code, see the [SimplePatchToolConsoleApp.Program.GenerateRSAKeyPair/SignXML/VerifyXML](SimplePatchToolConsoleApp/Program.cs) functions.

### E.4. Applying Patches

**Module:** SimplePatchToolCore

`public SimplePatchTool( string rootPath, string versionInfoURL )`: creates a new SimplePatchTool instance. Its parameters correspond to the *root* and *versionURL* arguments mentioned in the [QUICK START](#d4-applying-patches) section

`SimplePatchTool UseIncrementalPatch( bool canIncrementalPatch )`: sets whether or not incremental patches can be used to patch the application

`SimplePatchTool UseRepair( bool canRepair )`: sets whether or not repair patch can be used to patch the application

`SimplePatchTool VerifyFilesOnServer( bool verifyFiles )`: if *verifyFiles* is set to *true*, it corresponds to the *verifyFiles* argument mentioned in the [QUICK START](#d4-applying-patches) section

`SimplePatchTool UseCustomDownloadHandler( DownloadHandlerFactory factoryFunction )`: instructs SimplePatchTool to use a custom download handler. By default, [a WebClient based download handler is used](SimplePatchToolCore/Other/PatchDownloadManager.cs) but on some platforms (e.g. Unity), WebClient may not support *https* urls. In such cases, you may want to use a custom download handler implementation that supports *https*. **DownloadHandlerFactory** has the following signature: `delegate IDownloadHandler DownloadHandlerFactory()`

`SimplePatchTool UseCustomFreeSpaceCalculator( FreeDiskSpaceCalculator freeSpaceCalculatorFunction )`: by default, SimplePatchTool uses the *DriveInfo.AvailableFreeSpace* property to determine the free space of a drive but on some platforms (e.g. Unity), it may not be supported. In such cases, you may want to use a custom function to calculate the free space of a drive correctly (or, you can use a function that returns *long.MaxValue* to skip free space check entirely). **FreeDiskSpaceCalculator** has the following signature: `delegate long FreeDiskSpaceCalculator( string drive )`

`SimplePatchTool UseVersionInfoVerifier( XMLVerifier verifierFunction )`: instructs SimplePatchTool to verify the downloaded VersionInfo with the provided function. **XMLVerifier** has the following signature: `delegate bool XMLVerifier( ref string xmlContents )`. This function must return *true* only if the downloaded VersionInfo (**xmlContents**) is genuine. If you use a custom layer of security that e.g. encrypts the contents of the VersionInfo, you should first decrypt *xmlContents*

`SimplePatchTool UsePatchInfoVerifier( XMLVerifier verifierFunction )`: instructs SimplePatchTool to verify the downloaded PatchInfo's with the provided function. For example, the following code uses the *XMLSigner.VerifyXMLContents* function to verify the VersionInfo and PatchInfo files that have been signed with the *XMLSigner.SignXMLFile* function (or with the `Patcher sign_xml` console command):

```csharp
patcher.UseVersionInfoVerifier( ( ref string xml ) => XMLSigner.VerifyXMLContents( xml, publicRSAKey ) )
       .UsePatchInfoVerifier( ( ref string xml ) => XMLSigner.VerifyXMLContents( xml, publicRSAKey ) );
```

`SimplePatchTool LogProgress( bool value )`: sets whether or not SimplePatchTool should log any **IOperationProgress** data. This interface has two properties: `int Percentage { get; }` (between 0 and 100) and `string ProgressInfo { get; }` (localized description for the progress). Currently, two operations provide progress info: **DownloadProgress** and **FilePatchProgress**

`SimplePatchTool SilentMode( bool silent )`: sets whether or not SimplePatchTool should log anything (excluding **IOperationProgress** data)

`SimplePatchTool LogToFile( bool value )`: sets whether or not SimplePatchTool should write logs to a file. This log file will be located inside the cache directory with name *logs.dat*. Note that this file gets deleted when the cache directory is cleared after a successful patch

`bool CheckForUpdates( bool checkVersionOnly = true )`: checks whether or not app is up-to-date asynchronously in a separate thread. Returns *false*, if SimplePatchTool is already checking for updates or applying a patch. If **checkVersionOnly** is set to *true*, only the version number of the app (the file with *sptv* extension) is compared against the VersionInfo's version number. Otherwise, hashes and sizes of the files in the application directory are compared against VersionInfo (i.e. integrity check)

`bool Run( bool selfPatching )`: starts patching the application directory (*rootPath*) asynchronously in a separate thread. It is not mandatory to check for updates beforehand because this function internally checks for updates, as well. You should perform self patching only if you have a self patcher executable that was distributed with your application. This function returns *false*, if SimplePatchTool is already running

`string FetchLog()`: fetches the next log that SimplePatchTool has generated. Returns *null*, if there is no log in the queue

`IOperationProgress FetchProgress()`: returns an *IOperationProgress* instance if patcher's progress has changed, *null* otherwise

`void Cancel()`: cancels the current operation

`bool IsRunning { get; }`: returns true if SimplePatchTool is currently checking for updates or applying a patch

`PatchOperation Operation { get; }`: returns **CheckingForUpdates**, if last operation was/currently running is *CheckForUpdates*; **Patching**, if it was *Run(false)* and **SelfPatching**, if it was *Run(true)*

`PatchResult Result { get; }`: returns different values for different *Operation*'s. Its value should be checked after *IsRunning* returns *false*:

- **CheckingForUpdates**: returns *PatchResult.AlreadyUpToDate* if app is up-to-date, *PatchResult.Success* if there is an update for the app, and *PatchResult.Failed* if there was an error while checking for updates
- **Patching**/**SelfPatching**: returns *PatchResult.AlreadyUpToDate* if app is already up-to-date, *PatchResult.Success* if app is updated successfully, and *PatchResult.Failed* if there was an error while updating the app. In self patching mode, a value of *PatchResult.Success* means that we are ready to terminate the app and launch the self patcher executable (see *ApplySelfPatch* function below) 

`PatchStage PatchStage { get; }`: returns the current stage of the patcher (e.g. *CheckingUpdates*, *DownloadingFiles*, *DeletingObsoleteFiles* and so on)

`PatchFailReason FailReason { get; }`: if *Result* is *PatchResult.Failed*, this property stores why the patcher has failed (e.g. *Cancelled*, *InsufficientSpace*, *XmlDeserializeError* and so on). You may want to execute special logic for the following cases:

- **RequiresAdminPriviledges**: we need admin permissions to update the files in the application directory
- **FilesAreNotUpToDateAfterPatch**: after applying the patch, app is somehow still not up-to-date
- **UnderMaintenance_AbortApp**: servers are currently under maintenance and users should not be allowed to launch the app
- **UnderMaintenance_CanLaunchApp**: servers are currently under maintenance but users can continue using the app

`string FailDetails { get; }`: if *Result* is *PatchResult.Failed*, returns a localized string that briefly explains why the patcher has failed

`bool ApplySelfPatch( string selfPatcherExecutable, string postSelfPatchExecutable = null )`: terminates the app and runs the self patcher. You must pass the path of the self patcher executable to the **selfPatcherExecutable** parameter. If you'd like to launch an executable after self patcher executes successfully (e.g. to restart the app after self patching is complete), pass the path of that executable to the **postSelfPatchExecutable** parameter. This function should only be called if *Operation* is *SelfPatching* and *Result* is *Success*. Returns *false*, if something goes wrong

For example code, see the [SimplePatchToolConsoleApp.Program.ApplyPatch](SimplePatchToolConsoleApp/Program.cs) function.

SimplePatchTool follows this flow of execution while patching applications:

- download VersionInfo from the server
- if VersionInfo is encrypted/signed, decrypt/verify it (optional)
- check if servers are currently under maintenance (optional)
- compare hashes and sizes of the local files with VersionInfo to see if all files are up-to-date
- check if we need admin permissions to update the local files
- (*self patching*) check if user had previously terminated a running self patcher before it was finished and if so, rerun the self patcher
- determine the cheapest patch type
- check if there is enough free disk space
- (*incremental patching*) for each incremental patch to apply:
  - verify that PatchInfo for that incremental patch exists on the server (optional)
  - verify that diff files for this patch exist on the server and match the signature provided in VersionInfo (optional)
  - download the PatchInfo
  - if PatchInfo is encrypted/signed, decrypt/verify it (optional)
  - download the diff files as a compressed tar archive and decompress/extract it
  - apply diffs to local files
  - if there are any renamed files/folders in this patch, rename them (experimental, not documented yet)
- (*repair patching*) for each changed file in the version:
  - verify that the newest version of the file exists on the server and matches the signature provided in VersionInfo (optional)
  - download the file from the server and decompress it
  - replace local file with the decompressed file
- check if local files are now up-to-date. If somehow some files are still not up-to-date, try executing a repair patch
- delete any obsolete local files
- (*self patching*) create a *txt* file that holds basic commands for the self patcher to execute (like moving/deleting files)
- (*self patching*) terminate the app and run the self patcher executable

### E.5. Utilities

**Module:** SimplePatchToolCore

`static bool PatchUtils.CheckWriteAccessToFolder( string path )`: returns whether or not we have permission to write to the specified folder. SimplePatchTool automatically calls this function before downloading any patch files

`static string PatchUtils.GetCurrentExecutablePath()`: returns the path of the currently running executable (the application itself). Can be used to get the absolute path of the application directory or its value can be passed to the *postSelfPatchExecutable* parameter of the *SimplePatchTool.ApplySelfPatch* function to restart the app after self patching finishes

`static int PatchUtils.GetNumberOfRunningProcesses( string executablePath )`: returns the number of running instances of the specified executable. Can be used to check e.g. if another instance of this app is running or if the app/launcher that is to be updated is currently running. Before applying a patch, you should make sure that this is the only running instance of this app and any executables in the application directory are not running

In addition to these functions, there are some static variables and constants in the **PatchParameters** class that can be used to customize the default behaviour of SimplePatchTool. For example, hashes of files that are larger than **PatchParameters.FileHashCheckSizeLimit** are not calculated while checking whether or not they are genuine.

## F. EXTRAS

### F.1. Creating Self Patcher Executable

If you'd like to use the console-based self patcher that is provided with this library, simply rebuild the *SimplePatchToolSelfPatcher* project and ship the generated executable with your application. It is self-contained and is compatible with **.NET Standard 2.0**.

To create your own self patcher, you should first examine [SimplePatchToolSelfPatcher.Program](SimplePatchToolSelfPatcher/Program.cs) and understand how it works. I myself believe that a self patcher with a simple progress bar and a text that advises user against closing the self patcher would be more user friendly than a console app.

### F.2. Recommended Project Structure

If your project has a self patcher, it is recommended that you put it inside a subdirectory called *SPPatcher* (determined by *PatchParameters.SELF_PATCHER_DIRECTORY*) in your application directory, together with any of its dependencies. SimplePatchTool will always patch the contents of this directory manually (without using the self patcher). This way, you will be able to patch the self patcher itself.

If you are planning to use a small launcher app that will update and launch the main app, but also planning to support self patching for the launcher, then it is recommended that you put your main app and files that are used by it inside a subdirectory in your application directory. You can name this directory as you wish (let's say *MainApp*). Then, add `MainApp/` to the ignored paths of the launcher's patch. This way, MainApp will not be seen as an obsolete directory while self patching the launcher and files inside it will not be included in the launcher's patch. While creating patches for the main app or updating the main app from the launcher, use `{APPLICATION_DIRECTORY}\MainApp` as the root path. In conclusion:

- put main app with all its files inside a subdirectory (e.g. *MainApp*)
- put self patcher executable inside *SPPatcher* subdirectory
- create two patches: one for the launcher and one for the main app
- add `MainApp/` to the ignored paths of the launcher's patch
- use `{APPLICATION_DIRECTORY}\MainApp` as root path while creating the main app's patch and/or updating the main app
- inside your launcher, first check if a new version of the launcher is available. If so, patch it using self patching
- if launcher is up-to-date, check if a new version of the main app is available. If so, patch it without using self patching

### F.3. Localization

Currently, SimplePatchTool has built-in localization for English and Turkish languages. Localized strings are hard-coded into [SimplePatchToolCore.Localization](SimplePatchToolCore/Other/Localization.cs).

By default, *CultureInfo.CurrentCulture* is used to determine the language, but you can use the `Localization.SetCulture( CultureInfo culture )` or `Localization.SetLanguage( string languageISOCode )` functions to change the language manually. If the desired language is not available, these functions will return *false* and English localization will be used as fallback.

To provide your own localized strings to the localization system, use the `Localization.SetStrings( Dictionary<StringId, string> strings, string languageISOCode = null )` function. It doesn't matter how and where you store the localized strings, as long as you can build a **Dictionary<StringId, string>** with them. While creating that dictionary, you are recommended to use an instance of **Localization.StringIdComparer** as the dictionary's *IEqualityComparer* for better GC usage.

## G. ROADMAP

- add another patch method that packs all files in the version into a single compressed archive
- calculate percentage of the overall progress
- calculate the estimated remaining time
