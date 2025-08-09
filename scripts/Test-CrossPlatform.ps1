#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test script to validate cross-platform functionality of Sync-ConfluenceDoc.ps1

.DESCRIPTION
    This script performs basic validation checks to ensure the main script can run on different platforms.
#>

[CmdletBinding()]
param()

Write-Host "=== Cross-Platform Validation Test ===" -ForegroundColor Cyan

# Test 1: PowerShell Version
Write-Host "`n1. Testing PowerShell Version..." -ForegroundColor Yellow
$psVersion = $PSVersionTable.PSVersion
Write-Host "PowerShell Version: $psVersion"
if ($psVersion.Major -ge 7) {
    Write-Host "✓ PowerShell 7+ detected - Cross-platform compatible" -ForegroundColor Green
} elseif ($psVersion.Major -eq 6) {
    Write-Host "✓ PowerShell 6 detected - Cross-platform compatible" -ForegroundColor Green
} else {
    Write-Host "⚠ PowerShell $($psVersion.Major) detected - May have compatibility issues" -ForegroundColor Yellow
}

# Test 2: Platform Detection
Write-Host "`n2. Testing Platform Detection..." -ForegroundColor Yellow
if ($PSVersionTable.PSVersion.Major -ge 6) {
    Write-Host "Platform Variables Available:"
    Write-Host "  Windows: $isWindows" -ForegroundColor $(if ($isWindows) { "Green" } else { "Gray" })
    Write-Host "  Linux: $isLinux" -ForegroundColor $(if ($isLinux) { "Green" } else { "Gray" })
    Write-Host "  macOS: $isMacOS" -ForegroundColor $(if ($isMacOS) { "Green" } else { "Gray" })
} else {
    $platformWindows = $PSVersionTable.Platform -eq "Win32NT" -or $null -eq $PSVersionTable.Platform
    $platformLinux = $PSVersionTable.Platform -eq "Unix" -and $PSVersionTable.OS -like "*Linux*"
    $platformMacOS = $PSVersionTable.Platform -eq "Unix" -and $PSVersionTable.OS -like "*Darwin*"
    Write-Host "Platform Detection (Legacy):"
    Write-Host "  Windows: $platformWindows" -ForegroundColor $(if ($platformWindows) { "Green" } else { "Gray" })
    Write-Host "  Linux: $platformLinux" -ForegroundColor $(if ($platformLinux) { "Green" } else { "Gray" })
    Write-Host "  macOS: $platformMacOS" -ForegroundColor $(if ($platformMacOS) { "Green" } else { "Gray" })
}

# Test 3: .NET SDK Availability
Write-Host "`n3. Testing .NET SDK Availability..." -ForegroundColor Yellow
try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ .NET SDK found: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "⚠ .NET SDK not found - Some package installation may fail" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠ .NET SDK not available" -ForegroundColor Yellow
}

# Test 4: Directory Operations
Write-Host "`n4. Testing Directory Operations..." -ForegroundColor Yellow
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) "PowerShellCrossPlatformTest"
try {
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    
    # Test file creation
    $testFile = Join-Path $testDir "test.json"
    '{"test": "cross-platform"}' | Out-File -FilePath $testFile -Encoding utf8
    
    if (Test-Path $testFile) {
        Write-Host "✓ File operations working correctly" -ForegroundColor Green
        $content = Get-Content $testFile -Raw
        Write-Verbose "Test file content: $content"
    } else {
        Write-Host "✗ File operations failed" -ForegroundColor Red
    }
    
    # Cleanup
    Remove-Item $testDir -Recurse -Force
} catch {
    Write-Host "✗ Directory operations failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: Web Request Capability
Write-Host "`n5. Testing Web Request Capability..." -ForegroundColor Yellow
try {
    $testUrl = "https://httpbin.org/get"
    $response = Invoke-WebRequest -Uri $testUrl -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-Host "✓ Web requests working correctly" -ForegroundColor Green
    } else {
        Write-Host "⚠ Web request returned status: $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠ Web request test failed (this may be due to network/firewall): $($_.Exception.Message)" -ForegroundColor Yellow
}

# Test 6: JSON Operations
Write-Host "`n6. Testing JSON Operations..." -ForegroundColor Yellow
try {
    $testObject = @{
        title = "Test Document"
        content = "<p>Test content with special chars: àáâãäåæçèéêë</p>"
        timestamp = Get-Date
    }
    
    $json = ConvertTo-Json $testObject -Depth 5
    $parsed = ConvertFrom-Json $json
    
    if ($parsed.title -eq "Test Document") {
        Write-Host "✓ JSON operations working correctly" -ForegroundColor Green
    } else {
        Write-Host "✗ JSON operations failed" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ JSON operations failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Cross-platform validation completed." -ForegroundColor Green
Write-Host "If all tests passed, Sync-ConfluenceDoc.ps1 should work on your platform." -ForegroundColor Green
Write-Host "`nTo run the main script:" -ForegroundColor Yellow
if ($PSVersionTable.PSVersion.Major -ge 6) {
    if ($isWindows) {
        Write-Host "  .\Sync-ConfluenceDoc.ps1 -Mode Save -Verbose" -ForegroundColor Cyan
    } else {
        Write-Host "  pwsh ./Sync-ConfluenceDoc.ps1 -Mode Save -Verbose" -ForegroundColor Cyan
    }
} else {
    Write-Host "  .\Sync-ConfluenceDoc.ps1 -Mode Save -Verbose" -ForegroundColor Cyan
}