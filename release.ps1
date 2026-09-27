#Requires -Version 5.1
<#
.SYNOPSIS
    KSPPerformanceProfiler 一键发布脚本。
.DESCRIPTION
    依次执行：读取版本 -> Release 构建 -> 打包 zip -> git 提交/推送 -> 打 tag -> 发布 GitHub Release。
    发布优先使用 gh CLI；若未安装，则回退到 GITHUB_TOKEN / GH_TOKEN 调用 GitHub REST API。
.PARAMETER SkipBuild
    跳过 dotnet 构建，直接使用 bin\Release 中已有的产物。
.PARAMETER SkipGit
    跳过 git 提交 / 推送 / 打 tag，仅构建、打包并发布 Release。
.PARAMETER NotesFile
    使用指定的 Markdown 文件作为 Release 说明；默认根据 git 提交记录自动生成。
.EXAMPLE
    .\release.ps1
.EXAMPLE
    .\release.ps1 -SkipGit -NotesFile .\release-notes.md
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipGit,
    [string]$NotesFile
)

$ErrorActionPreference = 'Stop'

$RepoRoot      = $PSScriptRoot
$ModName       = 'KSPPerformanceProfiler'
$VersionFile   = Join-Path $RepoRoot "$ModName.version"
$ProjectFile   = Join-Path $RepoRoot "$ModName.csproj"
$DistDir       = Join-Path $RepoRoot 'dist'
$StageDir      = Join-Path $DistDir 'staging'
$GameDataStage = Join-Path $StageDir 'GameData'

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

# ---------- 1. 读取版本号 ----------
Write-Step '读取版本号'
$versionJson = Get-Content -LiteralPath $VersionFile -Raw | ConvertFrom-Json
$Version = '{0}.{1}.{2}' -f $versionJson.VERSION.MAJOR, $versionJson.VERSION.MINOR, $versionJson.VERSION.PATCH
$Tag     = "v$Version"
$ZipPath = Join-Path $DistDir "$ModName-$Version.zip"
Write-Host "    版本: $Version    Tag: $Tag"

# ---------- 2. 构建 ----------
if ($SkipBuild) {
    Write-Step '跳过构建 (-SkipBuild)'
} else {
    Write-Step '编译 Release'
    & dotnet build $ProjectFile -c Release
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build 失败。' }
}

$builtDll = Join-Path $RepoRoot "bin\Release\$ModName.dll"
if (-not (Test-Path -LiteralPath $builtDll)) { throw "未找到编译产物: $builtDll" }

# ---------- 3. 整理打包目录 ----------
Write-Step '整理打包目录'
if (Test-Path -LiteralPath $StageDir) { Remove-Item -LiteralPath $StageDir -Recurse -Force }
$modStage = Join-Path $GameDataStage $ModName
New-Item -ItemType Directory -Force -Path $modStage | Out-Null

Copy-Item -LiteralPath (Join-Path $RepoRoot 'Icons')        -Destination $modStage -Recurse
Copy-Item -LiteralPath (Join-Path $RepoRoot 'Localization') -Destination $modStage -Recurse
New-Item -ItemType Directory -Force -Path (Join-Path $modStage 'Plugins') | Out-Null
Copy-Item -LiteralPath $builtDll -Destination (Join-Path $modStage 'Plugins')
$builtPdb = Join-Path $RepoRoot "bin\Release\$ModName.pdb"
if (Test-Path -LiteralPath $builtPdb) {
    Copy-Item -LiteralPath $builtPdb -Destination (Join-Path $modStage 'Plugins')
}
Copy-Item -LiteralPath $VersionFile -Destination $modStage

# ---------- 4. 生成 zip 包 ----------
# 不使用 Compress-Archive：它在 Windows 上会写入反斜杠路径分隔符，
# 会导致 SpaceDock / Linux 端解压异常，这里手动写入正斜杠条目名。
Write-Step '生成 zip 包'
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($ZipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $GameDataStage -Recurse -File | ForEach-Object {
        $entryName = $_.FullName.Substring($StageDir.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $zip.Dispose()
}
Write-Host "    $ZipPath"

Push-Location $RepoRoot
try {
    # ---------- 5. git 提交 / 推送 / 打 tag ----------
    if ($SkipGit) {
        Write-Step '跳过 git 操作 (-SkipGit)'
    } else {
        Write-Step '提交并推送改动'
        if (git status --porcelain) {
            git add -A
            git commit -m "chore(release): $Tag"
            if ($LASTEXITCODE -ne 0) { throw 'git commit 失败。' }
        } else {
            Write-Host '    没有需要提交的改动。'
        }
        git push
        if ($LASTEXITCODE -ne 0) { throw 'git push 失败。' }

        Write-Step "创建并推送 tag $Tag"
        if (git tag -l $Tag) {
            Write-Host "    tag $Tag 已存在，跳过创建。"
        } else {
            git tag -a $Tag -m "$ModName $Tag"
            git push origin $Tag
            if ($LASTEXITCODE -ne 0) { throw '推送 tag 失败。' }
        }
    }

    # ---------- 6. 生成 Release 说明 ----------
    if ($NotesFile) {
        if (-not (Test-Path -LiteralPath $NotesFile)) { throw "找不到说明文件: $NotesFile" }
        $notes = Get-Content -LiteralPath $NotesFile -Raw
    } else {
        $prevTag = git tag --sort=-creatordate | Where-Object { $_ -ne $Tag } | Select-Object -First 1
        if ($prevTag) { $log = git log "$prevTag..HEAD" --oneline --no-merges } else { $log = git log --oneline -20 }
        $notes = "$ModName $Tag`n`n### Changes`n`n" + (($log | ForEach-Object { "- $_" }) -join "`n")
    }

    # ---------- 7. 发布 GitHub Release ----------
    Write-Step "发布 GitHub Release $Tag"
    $remote = git remote get-url origin
    if ($remote -notmatch 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/\.]+)') {
        throw "无法从 origin 解析 GitHub 仓库地址: $remote"
    }
    $owner = $Matches['owner']
    $repo  = $Matches['repo']

    New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
    $notesPath = Join-Path $DistDir 'release-notes.md'
    Set-Content -LiteralPath $notesPath -Value $notes -Encoding UTF8

    $gh    = Get-Command gh -ErrorAction SilentlyContinue
    $token = if ($env:GH_TOKEN) { $env:GH_TOKEN } else { $env:GITHUB_TOKEN }

    if ($gh) {
        gh release view $Tag *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    Release $Tag 已存在，改为上传/覆盖资源。"
            gh release upload $Tag $ZipPath --clobber
        } else {
            gh release create $Tag $ZipPath --title "$ModName $Tag" --notes-file $notesPath
        }
        if ($LASTEXITCODE -ne 0) { throw 'gh 发布 Release 失败。' }
    } elseif ($token) {
        $headers = @{
            Authorization = "Bearer $token"
            Accept        = 'application/vnd.github+json'
            'User-Agent'  = 'KSPPerformanceProfiler-release'
        }
        $existing = $null
        try {
            $existing = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$owner/$repo/releases/tags/$Tag"
        } catch {
            $existing = $null
        }
        if ($existing) {
            $releaseId = $existing.id
            Write-Host "    Release $Tag 已存在 (id=$releaseId)，改为上传资源。"
        } else {
            $payload = @{
                tag_name   = $Tag
                name       = "$ModName $Tag"
                body       = $notes
                draft      = $false
                prerelease = $false
            } | ConvertTo-Json
            $created = Invoke-RestMethod -Method Post -Headers $headers -ContentType 'application/json' `
                -Uri "https://api.github.com/repos/$owner/$repo/releases" -Body $payload
            $releaseId = $created.id
        }
        $assetName = Split-Path $ZipPath -Leaf
        Invoke-RestMethod -Method Post -Headers $headers -ContentType 'application/octet-stream' `
            -Uri "https://uploads.github.com/repos/$owner/$repo/releases/$releaseId/assets?name=$assetName" -InFile $ZipPath | Out-Null
    } else {
        throw '未检测到 gh CLI，也未设置 GITHUB_TOKEN / GH_TOKEN。请先安装 gh（winget install GitHub.cli）或设置访问令牌。'
    }

    Write-Host "`n完成: https://github.com/$owner/$repo/releases/tag/$Tag" -ForegroundColor Green
} finally {
    Pop-Location
}
