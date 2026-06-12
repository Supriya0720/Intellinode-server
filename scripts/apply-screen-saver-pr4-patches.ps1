# Applies PR4 compile-time patches to partial-class hosts and queue-path fixes.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$service = Join-Path $root 'src\Intellinode.Infrastructure\Services\WindowsScreenSaverSettingsService.cs'
$controller = Join-Path $root 'src\Intellinode.Api\Controllers\AdminWindowsScreenSaverController.cs'
$ack = Join-Path $root 'src\Intellinode.Infrastructure\Services\WindowsScreenSaverTaskAckHandler.cs'

function Patch-File($path, $replacements) {
    $text = Get-Content $path -Raw
    foreach ($pair in $replacements) {
        $text = $text -replace [regex]::Escape($pair.Old), $pair.New
    }
    Set-Content $path $text -NoNewline
}

Patch-File $service @(
    @{ Old = 'public sealed class WindowsScreenSaverSettingsService'; New = 'public sealed partial class WindowsScreenSaverSettingsService' },
    @{ Old = 'if (string.Equals(functionName, WindowsScreenSaverModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))'; New = 'if (WindowsScreenSaverModuleConstants.IsQueuedApplyFunctionName(functionName))' }
)

Patch-File $controller @(
    @{ Old = 'public sealed class AdminWindowsScreenSaverController'; New = 'public sealed partial class AdminWindowsScreenSaverController' }
)

$ackText = Get-Content $ack -Raw
$ackText = $ackText -replace '(?s)private static string MapTaskApplyMode\(string functionName\)\s*\{.*?\r?\n    \}', 'private static string MapTaskApplyMode(string functionName) =>`r`n        WindowsScreenSaverModuleConstants.MapApplyMode(functionName);'
Set-Content $ack $ackText -NoNewline

# Replace BuildQueueResponse in service with template-aware version (manual merge recommended).
Write-Host 'PR4 partial patches applied. Review BuildQueueResponse in WindowsScreenSaverSettingsService.cs for template metadata (see docs/windows-screen-saver-template-operations.md).'
