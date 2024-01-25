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
1. Open the Package Manager from `Window/Package Manager`
2. Click the '+' button in the top-left of the window
3. Click 'Add package from git URL'
4. Provide the URL of this git repository: https://github.com/nomnomab/lc-project-patcher.git
5. Click the 'add' button

## Usage

1. Use AssetRipper to export the game files
   - Set Script Export Format to `Decompiled`
   - Set Script Content Level to `Level 1`
2. Open the tool from `Tools/Nomnom/LC - Project Patcher`
3. Assign the Asset Ripper export directory path at the top
   - Example being `...\Lethal Company\ExportedProject\`
   - Do not include `Assets`
4. Assign the Game's data directory path at the top
    - Example being `C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\Lethal Company_Data`
5. Click `Install All` to run the install stage
   - This will restart Unity when it finishes to apply packages and enforce the New Input System
   - When it asks about switching the new backend to the New Input System, press Yes
6. Click `Fix All` to run the fix stage once you are back in the project
    - This will patch scripts, materials, etc
    - This will also copy the finished files into the project
    - This will probably take a while
7. Now you should have a nice template to work from!