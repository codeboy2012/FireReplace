Android SDK Platform Tools are NOT included in this repository.

Google distributes them under its own terms, so you download them yourself:

1. Download "SDK Platform-Tools for Windows" from
   https://developer.android.com/tools/releases/platform-tools
2. Extract the archive.
3. Copy the CONTENTS of the extracted platform-tools folder into THIS folder, so you end up with:

   FireReplace.exe
   platform-tools/adb.exe
   platform-tools/AdbWinApi.dll
   platform-tools/AdbWinUsbApi.dll

AdbWinApi.dll must be present: adb.exe will not start on Windows without it.

FireReplace looks for platform-tools/adb.exe next to FireReplace.exe (and in the folders above it
during development). If you already have a copy elsewhere, point
Settings > ADB > Platform tools location at it instead.

Never commit adb.exe or its DLLs to source control.
