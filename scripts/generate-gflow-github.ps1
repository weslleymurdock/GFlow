$ErrorActionPreference = 'Stop'

$RepositoryOwner = 'github'
$RepositoryName = 'rest-api-description'
$ApiDescriptionPath = 'descriptions/api.github.com'

$Root = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $Root 'src/GFlow.GitHub'
$GeneratedPath = Join-Path $ProjectPath 'Generated'
$OpenApiCachePath = Join-Path $PSScriptRoot '.github-openapi'

$Headers = @{
    Accept       = 'application/vnd.github+json'
    'User-Agent' = 'GFlow'
}

function Get-OpenApiVersions {
    $uri = "https://api.github.com/repos/$RepositoryOwner/$RepositoryName/contents/$ApiDescriptionPath"

    $items = Invoke-RestMethod `
        -Uri $uri `
        -Headers $Headers `
        -Method Get

    $versions = @(
        $items |
        Where-Object {
            $_.type -eq 'file' -and
            $_.name -match '^api\.github\.com\.(?<version>.+)\.json$'
        } |
        ForEach-Object {
            [PSCustomObject]@{
                Version     = $_.name -replace '^api\.github\.com\.(.+)\.json$', '$1'
                Name        = $_.name
                DownloadUrl = $_.download_url
            }
        } |
        Sort-Object Version -Descending
    )

    if ($versions.Count -eq 0) {
        throw 'No GitHub OpenAPI versions were found.'
    }

    return $versions
}

function Select-OpenApiVersion {
    param(
        [Parameter(Mandatory)]
        [array]$Versions
    )

    $selectedIndex = 0

    try {
        [Console]::CursorVisible = $false
    }
    catch {
    }

    while ($true) {
        Clear-Host

        Write-Host 'GFlow GitHub REST API Generator' -ForegroundColor Cyan
        Write-Host
        Write-Host 'Select the GitHub OpenAPI version:' -ForegroundColor White
        Write-Host 'Use Up/Down to navigate and Enter to select.' -ForegroundColor DarkGray
        Write-Host

        for ($index = 0; $index -lt $Versions.Count; $index++) {
            $version = $Versions[$index]

            if ($index -eq $selectedIndex) {
                Write-Host '  > ' -NoNewline -ForegroundColor Magenta
                Write-Host $version.Version -ForegroundColor Magenta
            }
            else {
                Write-Host '    ' -NoNewline
                Write-Host $version.Version
            }
        }

        $key = [Console]::ReadKey($true)

        switch ($key.Key) {
            'UpArrow' {
                if ($selectedIndex -gt 0) {
                    $selectedIndex--
                }
                else {
                    $selectedIndex = $Versions.Count - 1
                }
            }

            'DownArrow' {
                if ($selectedIndex -lt ($Versions.Count - 1)) {
                    $selectedIndex++
                }
                else {
                    $selectedIndex = 0
                }
            }

            'Enter' {
                try {
                    [Console]::CursorVisible = $true
                }
                catch {
                }

                return $Versions[$selectedIndex]
            }

            'Escape' {
                try {
                    [Console]::CursorVisible = $true
                }
                catch {
                }

                throw 'Generation cancelled by user.'
            }
        }
    }
}

function Get-OpenApi {
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Version
    )

    if (-not (Test-Path $OpenApiCachePath)) {
        New-Item `
            -ItemType Directory `
            -Path $OpenApiCachePath `
            -Force |
        Out-Null
    }

    $fileName = $Version.Name
    $outputPath = Join-Path $OpenApiCachePath $fileName

    Write-Host
    Write-Host "Downloading GitHub OpenAPI $($Version.Version)..." -ForegroundColor Cyan

    Invoke-WebRequest `
        -Uri $Version.DownloadUrl `
        -Headers $Headers `
        -OutFile $outputPath

    return $outputPath
}

function Get-GitHubClient {
    param(
        [Parameter(Mandatory)]
        [string]$OpenApiPath
    )

    if (-not (Test-Path $ProjectPath)) {
        throw "GFlow.GitHub project was not found: $ProjectPath"
    }

    if (Test-Path $GeneratedPath) {
        Write-Host
        Write-Host "Removing existing generated client..." -ForegroundColor DarkGray

        Remove-Item `
            -Path $GeneratedPath `
            -Recurse `
            -Force
    }

    Write-Host
    Write-Host 'Generating complete GitHub REST client with Kiota...' -ForegroundColor Cyan
    Write-Host

    dotnet tool restore

    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to restore local .NET tools.'
    }

    dotnet tool run kiota generate `
        --language CSharp `
        --openapi $OpenApiPath `
        --output $ProjectPath `
        --class-name GitHubClient `
        --namespace-name GFlow.GitHub `
        --clean-output

    if ($LASTEXITCODE -ne 0) {
        throw 'Kiota failed to generate the GitHub REST client.'
    }
}

try {
    $versions = @(Get-OpenApiVersions)

    $selectedVersion = Select-OpenApiVersion -Versions $versions

    Write-Host
    Write-Host "Selected OpenAPI version: $($selectedVersion.Version)" -ForegroundColor Magenta

    $openApiPath = Get-OpenApi -Version $selectedVersion

    Get-GitHubClient -OpenApiPath $openApiPath

    Write-Host
    Write-Host 'GitHub REST client generated successfully.' -ForegroundColor Green
    Write-Host
    Write-Host "OpenAPI:  $($selectedVersion.Version)"
    Write-Host "Source:   $openApiPath"
    Write-Host "Output:   $ProjectPath"
}
catch {
    try {
        [Console]::CursorVisible = $true
    }
    catch {
    }

    Write-Host
    Write-Host "Generation failed: $($_.Exception.Message)" -ForegroundColor Red

    exit 1
}
finally {
    try {
        [Console]::CursorVisible = $true
    }
    catch {
    }
}
