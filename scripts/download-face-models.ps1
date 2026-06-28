# Downloads @vladmandic/face-api browser bundle + ML models into wwwroot for offline gate use.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$npmDir = Join-Path $root 'tools\npm-face-api'
$modelDest = Join-Path $root 'src\SMS.Web\wwwroot\models\face-api'
$jsDest = Join-Path $root 'src\SMS.Web\wwwroot\js\face-api.min.js'

New-Item -ItemType Directory -Force -Path $modelDest | Out-Null
npm install @vladmandic/face-api@1.7.15 --prefix $npmDir --no-save
$pkg = Join-Path $npmDir 'node_modules\@vladmandic\face-api'
Copy-Item -Path (Join-Path $pkg 'model\*') -Destination $modelDest -Force
Invoke-WebRequest -Uri 'https://cdn.jsdelivr.net/npm/@vladmandic/face-api/dist/face-api.min.js' -OutFile $jsDest
Write-Host "Face-api models and js copied to wwwroot ($(@(Get-ChildItem $modelDest).Count) model files)."
