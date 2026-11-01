param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$skills=Get-Content (Join-Path $root 'skill.json') -Raw | ConvertFrom-Json -AsHashtable
$ids=@($skills.Keys | Sort-Object { $skills[$_].layout.row }, { $skills[$_].layout.column })
function Literal([string]$value) { return ('@"'+$value.Replace('"','""')+'"') }
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('// Generated from skill.json by tools/GenerateSkills.ps1. Do not edit.')
$lines.Add('namespace FenceExpansion { internal static class SkillDefinitions { internal static void Register() {')
foreach($id in $ids){
 $s=$skills[$id]
 if(!(Test-Path -LiteralPath (Join-Path $root ('assets/'+$s.icon)))){throw "Missing icon $id"}
 $pre= if($s.prerequisites.Count) {'new string[] { '+ (($s.prerequisites|ForEach-Object{Literal ($(if ($_ -like 'skill_*') {'fence_expansion.'+$_} else {$_}))}) -join ',')+' }'}else{'null'}
 $lines.Add('ModHelper.AddNetworkUpgrade(ModEntry.Id, '+(Literal ('fence_expansion.'+$id))+', SkillConfig.Cost('+(Literal $id)+', '+$s.cost+'), "Fence", () => ModEntry.Text('+(Literal $s.name)+','+(Literal $s.name_en)+'), () => ModEntry.Text('+(Literal $s.description)+','+(Literal $s.description_en)+'), () => {}, tab: ModEntry.Id, iconKey: '+(Literal ('fence_expansion:' + [IO.Path]::GetFileNameWithoutExtension($s.icon)))+', prerequisites: '+$pre+', subtitleLabel: () => ModEntry.Text("销赃人", "Fence"));')
}
$lines.Add('} internal static readonly System.Collections.Generic.Dictionary<string,int> DefaultCosts = new System.Collections.Generic.Dictionary<string,int> {')
foreach($id in $ids){$lines.Add('{ '+(Literal $id)+', '+$skills[$id].cost+' },')}
$lines.Add('}; } }')
Set-Content -LiteralPath (Join-Path $root 'src/FenceExpansion/SkillDefinitions.Generated.cs') -Value $lines -Encoding utf8



