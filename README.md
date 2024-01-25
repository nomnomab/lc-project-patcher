# Lethal Company Project Patcher

This tool fills in a unity project with functional assets so it can be used for Lethal Company modding.

### What does it do?

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

<br/>

#### todo
Make instructions to build the stripping part of the tool