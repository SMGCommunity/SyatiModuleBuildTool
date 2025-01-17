# SyatiModuleBuildTool
A program to build Syati Modules. (Command line tool)


```bat
SyatiModuleBuildTool.exe <REGION> <Path_To_Syati> <Path_To_Modules_Folder> <Path_To_Code_Output_Folder> <Path_To_Disc_Output_Folder>
```
- Replace `<REGION>` with one of the following: `USA`, `PAL`, `JPN`, `KOR`, `TWN`. Choose the one that matches your game disc.
- Replace `<Path_To_Syati>` with the complete path to your Syati folder. If your path has spaces in it, surround it with Quotation Marks "
- Replace `<Path_To_Modules>` with the complete path to the folder that you put the modules into. If your path has spaces in it, surround it with Quotation Marks "
- Replace `<Path_To_Code_Output_Folder>` with the complete path of the folder that you would like the resulting CustomCode.bin to be saved to. If your path has spaces in it, surround it with Quotation Marks "
- Replace `<Path_To_Disc_Output_Folder>` with the complete path of the folder that you would like the disc files of the modules to be copied to. If your path has spaces in it, surround it with Quotation Marks "
- If you would like to use **UniBuild**, add `-u` to the end of the command. (Ensure there is a space between the last path and the `-u`)
  > *Note: UniBuild is an alternative method of compiling modules which may result in smaller output binaries. If you have less than 10 modules, you do not need UniBuild.*

After running the command, you will be given a **CustomCode.bin** and a **CustomCode.map**.
