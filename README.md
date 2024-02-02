# Lethal Company Project Patcher

> This tool is still in development and is quite experimental, but should be usable.

This tool fills in a unity project with functional assets so it can be used for Lethal Company modding.

## What does it do?

- Installs required packages, and enforces specific versions for them
- Updates various project settings:
  - Tags
  - Layers
  - Physics settings
  - Time settings
  - Navmesh settings
- Strips game scripts so they can compile in Unity
- Fixes missing script references
- Fixes missing shaders on materials
- Fixes broken Scriptable Objects
- Copies needed DLLs from the game directly
- Copies needed files from the Asset Ripper exported project

![image](./Images~/preview.png)

## Installation
#### Using Unity Package Manager

1. Make sure you have git installed: https://git-scm.com/download/win
    - After installing, restart Unity
2. Open the Package Manager from `Window/Package Manager`
3. Click the '+' button in the top-left of the window
4. Click 'Add package from git URL'
5. Provide the URL of the this git repository: https://github.com/nomnomab/lc-project-patcher.git
6. Click the 'add' button

## Usage

1. Create a new Unity project
    - Use version 2022.3.9f1
    - Use the 3D (HDRP) template
2. Open the tool from `Tools/Nomnom/LC - Project Patcher`

> At this point if you have the DunGen asset, or any other asset, import it now and move it into `Unity\AssetStore`

3. Assign the Game's data directory path at the top
    - Example being `C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\Lethal Company_Data`
4. Click the `Run Patcher` button
7. Now you should have a nice template to work from!

## Notes

- If you have DunGen in the project (in the default location) then it will use that for any DunGen-related guids instead of the stubs
  - Make sure DunGen is in the project *before* using the tool

## Credits

- Asset Ripper - https://github.com/AssetRipper/AssetRipper
  - Modified source - https://github.com/nomnomab/AssetRipper
- Asset Bundles Browser - https://github.com/Unity-Technologies/AssetBundles-Browser
- UniTask - https://github.com/Cysharp/UniTask