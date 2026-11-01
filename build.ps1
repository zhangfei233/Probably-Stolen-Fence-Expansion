param([switch]$InstallToCopy, [switch]$RuntimeProbe, [string]$GameManaged = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'tools/GenerateSkills.ps1')
$env:DOTNET_CLI_HOME = Join-Path $root '.build-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MSBuildEnableWorkloadResolver = 'false'
$extra=@()
if($GameManaged){
    $GameManaged=(Resolve-Path -LiteralPath $GameManaged).Path
    if(!(Test-Path -LiteralPath (Join-Path $GameManaged 'Assembly-CSharp.dll'))){throw 'GameManaged must point to the game Managed directory.'}
    $extra += "-p:GameManaged=$GameManaged"
}
if($RuntimeProbe -and !(Test-Path (Join-Path $root 'tools/RuntimeProbe.cs'))){throw 'RuntimeProbe is a local development tool, not included in the public source.'}
& dotnet build (Join-Path $root 'src\FenceExpansion\FenceExpansion.csproj') -c Release --ignore-failed-sources "-p:FenceRuntimeProbe=$($RuntimeProbe.IsPresent.ToString().ToLowerInvariant())" @extra
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$package = Join-Path $root 'dist\FenceExpansion'
New-Item -ItemType Directory -Force -Path $package | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'src\FenceExpansion\bin\Release\netstandard2.1\FenceExpansion.dll') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'src\FenceExpansion\manifest.xml') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'RELEASE.md') -Destination (Join-Path $package 'README.md')
$texturePath = Join-Path $package 'Textures/Items'
New-Item -ItemType Directory -Force -Path $texturePath | Out-Null
Copy-Item -Path (Join-Path $root 'assets/skill_*.png') -Destination $texturePath -Force
foreach($name in @('apple','bottle','ender_eye')) {
    Copy-Item -LiteralPath (Join-Path $root "assets/$name.png") -Destination $texturePath -Force
}
foreach($name in @('ender_frame','enderman')) {
    Copy-Item -LiteralPath (Join-Path $root "assets/$name.png") -Destination (Join-Path $package "Textures/$name.png") -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $package 'Textures/Paper') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'assets/ender_frame.png') -Destination (Join-Path $package 'Textures/Paper/ender_frame.png') -Force
Copy-Item -LiteralPath (Join-Path $root 'assets/master thief.png') -Destination (Join-Path $package 'Textures/master_thief.png') -Force
$publicSkills=Get-Content (Join-Path $root 'skill.json') -Raw | ConvertFrom-Json -AsHashtable
foreach($skill in $publicSkills.Values){$skill.Remove('effect')}
$publicSkills | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $package 'skill.json') -Encoding utf8
if ($InstallToCopy) {
    $destination = Join-Path $root 'Probably Stolen_copy\Mods\FenceExpansion'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $package '*') -Destination $destination -Force -Recurse
    Write-Output "Installed in development copy: $destination"
}
Write-Output "Package: $package"



