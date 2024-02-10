# Lethal Company Project Patcher

> This tool is still in development and is quite experimental, but should be usable.

This tool fills in a unity project with functional assets so you can run the game in the editor to test custom plugins.

This tool does **not** distribute game files. It uses what is already on your computer from the installed game.

## What does it do?

- Installs required packages, and enforces specific versions for them
- Updates various project settings:
  - Tags
  - Layers
  - Physics settings
  - Time settings
  - Navmesh settings
- Strips generated netcode inside of game scripts so they can compile in Unity
- Fixes missing script references
- Fixes missing shaders on materials
- Fixes broken scriptable objects
- Copies needed DLLs from the game directly
- Exports game assets with an embeded version of Asset Ripper
- Sets up a BepInEx environment to test patches and plugins in-editor
- Can load up normal plugins in-editor
- Supports disabling domain reloading in-editor for faster compile times
- And much more!

![image](./Images~/preview_3.png)

## Requirements

- About 900 MB for the asset ripper export
  - There is a toggle to delete the export after the patcher is done
- About 900 MB for the copied files from that export into the project
- [Git](https://git-scm.com/download/win)
- [Unity 2022.3.9f1](https://unity.com/releases/editor/archive)
- [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  - For running Asset Ripper

[//]: # (- [AssetRipper.SourceGenerated.dll]&#40;https://github.com/nomnomab/AssetRipper/releases&#41;)

## Installation
#### Using Unity Package Manager

1. Make sure you have git installed: https://git-scm.com/download/win
    - After installing, restart Unity
2. Open the Package Manager from `Window > Package Manager`
3. Click the '+' button in the top-left of the window
4. Click 'Add package from git URL'
5. Provide the URL of the this git repository: https://github.com/nomnomab/lc-project-patcher.git#v0.3.0
6. Click the 'add' button

## Usage

1. Create a new Unity project
    - Use version [2022.3.9f1](https://unity.com/releases/editor/archive)
    - Use the 3D (HDRP) template
2. Open the tool from `Tools > Nomnom > LC - Project Patcher > Open`
    - This will create some default folders for you when it opens

[//]: # (3. Download `AssetRipper.SourceGenerated.dll.zip` from the releases of https://github.com/nomnomab/AssetRipper/releases)

[//]: # (   - Extract the dll from the zip)

[//]: # (   - Place into `[ProjectName]\Library\PackageCache\com.nomnom.lc-project-patcher@[SomeNumbers]\Editor\Libs\AssetRipper~`)

> At this point if you have the DunGen asset, or any other asset store asset, import it now and move it into `Assets\Unity\AssetStore`. 
> This is the location the patcher checks for existing assets if needed.

3. Assign the Game's data directory path at the top
    - Example being `C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\Lethal Company_Data`
4. Click the `Run Patcher` button
    - This process *will* take a while, so be patient
    - The editor may restart a few times, this is normal
    - When it asks about the New Input System and switching backends, click `Yes`
5. Now you should have a nice template to work from!

## BepInEx Usage

You can make plugins directly in the editor like normal.

> I have not tested *patchers*, so use them with caution!

If you want to add some normal plugins, you'll have to navigate outside of `Assets` for this, and next to it is a folder called `Lethal Company`.

This is a dummy folder that houses the normal BepInEx root and a fake game data structure so it can initialize before I route it to the actual game files.

## Project Structure

- `[ProjectName]\Assets\LethalCompany\Game` - The ripped game assets in an easier to navigate folder structure
- `[ProjectName]\Assets\LethalCompany\Mods` - A common folder to place custom plugins or plugins you want to reference
- `[ProjectName]\Assets\LethalCompany\Tools` - BepInEx, MonoMod, and any other "core" dll goes here
- `[ProjectName]\Assets\Unity\AssetStore` - Where assets from the asset store should go to keep them nice and tidy
  - Also where the tool picks up DunGen from to use for guids instead of the one provided by the game
  - This folder gets re-imported when the tool runs to fix any weird asset issues 
- `[ProjectName]\Assets\Unity\Native` - The default files/folders that were in the `Assets` root when running the tool
- `[ProjectName]\Lethal Company\BepInEx` - The normal BepInEx directory people are used to
- `[ProjectName]\*.cfg` - Where the editor version of BepInEx places config files

## FAQ

### Can I use my normal BepInEx folder in the game directory?

Yep! Just turn on the option located at `Tools > Nomnom > LC - Project Patcher > Use Game BepInEx Directory`.

Afterward, you should restart Unity if you already have some plugins loaded up to unload them.

### Can I transfer assets from another project into this after patching?

Yes, but you'll have to do it manually.

1. Copy the code from https://github.com/nomnomab/lc-project-patcher/blob/v0.3.0/Editor/ExtractProjectInformationUtility.cs and paste it into a new script in the project with your existing assets called `ExtractProjectInformationUtility.cs`
2. Run the type extractor from `Tools > Nomnom > LC - Project Patcher > Extract Project Information`
3. Once this is done, it will make a json file with the required information at the location you specified
4. Go back to the new project
5. Copy over any assets from the old project to the new project
6. Run the asset patcher from `Tools > Nomnom > LC - Project Patcher > Patch Assets From Other Projects...`
5. Select the json file you made from the other project
6. Wait for the patcher to complete
7. Now your prefabs/scenes should be migrated over properly

If there are any "missing prefab" issues in your prefabs/scenes, then make sure you have the needed assets in the project.

### Why can I not delete a plugin from the plugins folder?

If a plugin is loaded while playing the game in-editor, then it will stay loaded forever. This is just how Unity handles their dll hooks.

If you need to remove a plugin, then close unity first, then delete it.

The same steps go for anything else that BepInEx has a hook on, such as its log files.

### Why is my audio doubling?

For some reason the diagetic audio mixer's master group has effects on by default. The main reason why there is an echo
is due to the `Echo` effect on it. 

If the auto-patcher didn't work, you can remove this effect by:

1. Opening the `Diagetic` audio mixer
2. Clicking on the `Master` group
3. Going into the inspector and navigate to where `Echo` is
4. Click the gear in the top-right of the effect and click `Bypass`
5. Profit

### How can I make Unity not take three years to compile scripts when pressing play?

> Make sure you understand what you have to do manually if domain reloading is disabled
> 
> https://docs.unity3d.com/Manual/DomainReloading.html

This is straightforward, as long as your own plugin code supports it.

Not all plugins support this by the way, so expect errors with ones that don't handle static values properly.

1. Open the `Edit > Project Settings` menu
2. Go to the `Editor` tab
3. Check `Enter Play Mode Options`
4. Check `Reload Scene`

Now it will take like a second to press play instead of a minute 😀

### Why doesn't my plugin get found from inside a folder with an assembly definition?

I only check Assembly-CSharp.dll for in-editor plugins at the moment.

### How can I add MMHOOK_Assembly-CSharp.dll

For now get it normally via the normal game and the patcher approach. Once you have the dll, put it into the plugins directory.

- The default location is `[ProjectName]\Lethal Company\BepInEx\plugins`

## Useful packages

- Parrel Sync - https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync
  - This lets you run multiple instances of the project at once to test LAN without building the game.

- Editor Attributes - https://github.com/v0lt13/EditorAttributes.git
  - This is a nice package that adds some useful attributes for the inspector

## Credits

- Asset Ripper - https://github.com/AssetRipper/AssetRipper
  - Modified source - https://github.com/nomnomab/AssetRipper
- Asset Bundles Browser - https://github.com/Unity-Technologies/AssetBundles-Browser
- UniTask - https://github.com/Cysharp/UniTask
- UYAML Parser - https://gist.github.com/Lachee/5f80fb5cb2be99dad9fc1ae5915d8263
- MonoMod - https://github.com/MonoMod/MonoMod
- GameViewSizeShortcut - https://gist.github.com/wappenull/668a492c80f7b7fda0f7c7f42b3ae0b0
- BepInEx - https://github.com/BepInEx/BepInEx

<br/>

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/B0B6R2Z9U)