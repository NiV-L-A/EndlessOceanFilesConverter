# Endless Ocean Files Converter

## Description
EndlessOceanFilesConverter is a tool that can convert multiple file formats from Endless Ocean 1 & 2 into more common ones. <br>
Please, refer to the [Changelog](/EndlessOceanFilesConverter-Changelog.txt) file for information about changes for each version.

Supported file formats:
- .mdl -> .glb + .png (Model)
- .hit -> .glb (Hitbox)
- .tdl -> .png (Texture)
- .pak -> Converts the files found inside (Archive of files, usually textures except for "md112mot.pak" which contains exclusively .mot files)
- .txs -> Converts the files found inside (Archive of textures)
- .rtl -> To be passed along with the Endless Ocean 2 bNNrodMM.mdl files (Tranformation data used to correctly place each "rod" model)
- .mot -> To be passed along with the .mdl files that require external animation data
- GAME.DAT + INFO.DAT -> Extracts the files inside GAME.DAT and outputs them in a folder named 'out' (created in the same folder where GAME.DAT is).
  - To use this function, only pass to the exe the GAME.DAT and INFO.DAT files.

## How to run / Troubleshooting
**IMPORTANT:** *.NET Desktop Runtime 9.0 x64* must be installed in order to run this program.<br>
https://dotnet.microsoft.com/en-us/download/dotnet/9.0

To use this tool, drag and drop onto the exe:
- a file,
- multiple files,
- a folder,
- multiple folders,

containing one or more of the supported file formats. <br>

## Notes
- EO1's main map (stage/sea/s01fNNMM) models and hitboxes are located at 0,0,0 by default. The tool will recognize s01fNNMM files by their file name and will apply the needed translation to them.
- EO2's main maps (st/b01, st/b02 and st/b03) models are located at 0,0,0 by default.
The tool will recognize bNNrodMM.mdl files by their file name and will apply the needed translation to them only if their matching bNNstage.rtl file is also passed. <br>
  - For this reason, st/b01, st/b02 and st/b03 must be parsed individually.
- Every node in the hierarchy list is prefixed with a number and an underscore.
The number represents a progressive index of each node, and it is there to ensure that the order of the hierarchy list is maintained. <br>
- Texture files that come from a .mdl will be named with a prefix,
this being the name of the mdl itself (for example: d529.mdl contains the texture "tanatos.tdl", and the file name created will be "d529_tanatos.tdl.png").
  - This was done to ensure uniqueness (for example, b14stage.mdl contains b14itm11.tdl, but also b14obj20.mdl contains b14itm11.tdl, but they are not the same texture).
- Both games have hardcoded names that are used to parse some models. The following is a list that should be followed when using this tool.
  - For example: b10stage.mdl uses the mesh from b10obj00.mdl. The tool will check if both files are passed to the exe, and if so, will not dump b10obj00.mdl as a glb file
  and instead place b10obj00's mesh at a specific node ("b10_obj00") in b10stage's hierarchy list. <br>
  *If b10obj00.mdl is passed but b10stage.mdl is not, the tool will dump b10obj00.mdl as a glb file.*
  - EO1
    - The "pl/00" folder (not only its contents) must be passed to correctly dump pl/00/p00.mdl
    - The "pl/10" folder (not only its contents) must be passed to correctly dump pl/10/p10.mdl
  - EO2
    - ch/p00.mdl needs the "body", "boot", "face", etc... folders
    - ch/p01.mdl needs the "body", "boot", "face", etc... folders
    - ch/p02.mdl needs the "body", "boot", "face", etc... folders
    - ch/p10.mdl needs the "body", "boot", "face", etc... folders
    - ch/p11.mdl needs the "body", "boot", "face", etc... folders
    - ch/p12.mdl needs the "body", "boot", "face", etc... folders
    - ch/p20.mdl needs p20b000.mdl
    - ch/p30.mdl needs p30b000.mdl
    - ch/p32.mdl needs p30b000.mdl
    - ch/p40.mdl needs p40b100.mdl
    - ch/p42.mdl needs p40b100.mdl
    - ch/p50.mdl needs p50b200.mdl
    - ch/p52.mdl needs p50b200.mdl
    - ch/p60.mdl needs p60b000.mdl
    - b01rodNN.mdl need b01stage.rtl, b01pmset.mdl, the ms/ and the ps/ folders
    - b02rodNN.mdl need b02stage.rtl and b02pmset.mdl
    - b03rodNN.mdl need b03stage.rtl, b03pmset.mdl, the ms/ and the ps/ folders
    - b07stage.mdl needs b07pmset.mdl
    - b08stage.mdl needs b08pmset.mdl
    - b10stage.mdl needs b10obj00.mdl 
    - b14stage.mdl needs b14objNN.mdl and palmNN.mdl files
    - b17stage.mdl needs the ms/ folder
    - Each dolphin model inside ch/dolphin needs life/dolphin/md112mot.pak and its unique .mot found in ch/dolphin
    - Each dolphin model inside life/dolphin needs life/dolphin/md112mot.pak and its unique .mot found in life/dolphin

## TO-DO
- If an animation does not have transformation data of a bone, that bone will get the current transformation at the time of animation switching
- Sharks' attack animations do not slow down like seen in game.
- Issues with meshes that have transparency
- Parse "lifeset" files
- Parse ".scp" files
- Implement multitexturing support for EO2 (not currently supported in gltf)

## Credits
Author:
- NiV-L-A

Special thanks:
- MDB
- Hiroshi
- vpenades for SharpGLTF: https://github.com/vpenades/SharpGLTF
- SixLabors for ImageSharp: https://github.com/SixLabors/ImageSharp
- Custom Mario Kart Wiiki for TPL file format documentation: https://wiki.tockdom.com/wiki/Image_Formats