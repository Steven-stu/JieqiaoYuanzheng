# Reassemble split BGM files.
# Usage: powershell -ExecutionPolicy Bypass -File tools\reassemble_bgm.ps1
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outDir = Join-Path $repo "audio\Bgm_reassembled"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
Get-ChildItem (Join-Path $repo "audio\bgm_parts") -Directory | Sort-Object Name | ForEach-Object {
  $target = $_.Name
  $parts = Get-ChildItem $_.FullName -File | Sort-Object Name
  if ($parts.Count -eq 0) { return }
  $base = ($parts[0].Name -replace '\.part\d+$', '')
  $ms = New-Object System.IO.MemoryStream
  foreach ($p in $parts) {
    $b = [System.IO.File]::ReadAllBytes($p.FullName)
    $ms.Write($b, 0, $b.Length)
  }
  [System.IO.File]::WriteAllBytes((Join-Path $outDir $base), $ms.ToArray())
  $ms.Dispose()
  Write-Output ("reassembled " + $base)
}