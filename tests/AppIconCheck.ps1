param([Parameter(Mandatory)][string[]]$Executables)
$ErrorActionPreference = 'Stop'
$iconPath = Join-Path $PSScriptRoot '..\PlayWin.WinUI\Assets\PlayWin.ico'
Add-Type -AssemblyName System.Drawing
if (-not ('PlayWinIconCheck' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PlayWinIconCheck {
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    public static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr icon);
}
'@
}
function Get-IconPixels([string]$path) {
    $large = [IntPtr]::Zero; $small = [IntPtr]::Zero
    if ([PlayWinIconCheck]::ExtractIconEx($path, 0, [ref]$large, [ref]$small, 1) -eq 0) { throw "No embedded icon: $path" }
    try {
        $icon = [Drawing.Icon]::FromHandle($large)
        $bitmap = $icon.ToBitmap()
        try {
            $pixels = [Collections.Generic.List[int]]::new()
            $pixels.Add($bitmap.Width); $pixels.Add($bitmap.Height)
            for ($y = 0; $y -lt $bitmap.Height; $y++) {
                for ($x = 0; $x -lt $bitmap.Width; $x++) { $pixels.Add($bitmap.GetPixel($x, $y).ToArgb()) }
            }
            return ($pixels -join ',')
        }
        finally { $bitmap.Dispose(); $icon.Dispose() }
    }
    finally {
        if ($large -ne [IntPtr]::Zero) { [PlayWinIconCheck]::DestroyIcon($large) | Out-Null }
        if ($small -ne [IntPtr]::Zero) { [PlayWinIconCheck]::DestroyIcon($small) | Out-Null }
    }
}
$expected = Get-IconPixels ([IO.Path]::GetFullPath($iconPath))
foreach ($exe in $Executables) {
    if ((Get-IconPixels ([IO.Path]::GetFullPath($exe))) -ne $expected) { throw "Executable does not embed the PlayWin B icon: $exe" }
    'PASS embedded PlayWin B icon: ' + [IO.Path]::GetFileName($exe)
}
