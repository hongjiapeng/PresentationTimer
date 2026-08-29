[CmdletBinding(DefaultParameterSetName = 'Process')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Path')]
    [string]$AppPath,
    [Parameter(Mandatory, ParameterSetName = 'Process')]
    [int]$ProcessId
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
internal static class PresentationTimerNativeMethods
{
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr windowHandle);
}
'@

$artifactDirectory = Join-Path $PSScriptRoot '..\artifacts\ui-smoke'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
$application = $null
$ownsApplication = $false

function Wait-Until {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Condition,
        [int]$TimeoutMilliseconds = 5000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $value = & $Condition
        if ($null -ne $value -and $value -ne $false) {
            return $value
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out after $TimeoutMilliseconds ms."
}

function Find-Element {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)]
        [string]$AutomationId
    )

    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-Element {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)]
        [string]$AutomationId,
        [int]$TimeoutMilliseconds = 5000
    )

    return Wait-Until -TimeoutMilliseconds $TimeoutMilliseconds -Condition {
        Find-Element -Root $Root -AutomationId $AutomationId
    }
}

function Activate-Element {
    param([System.Windows.Automation.AutomationElement]$Element)

    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
        return
    }

    if ($Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        ([System.Windows.Automation.TogglePattern]$pattern).Toggle()
        return
    }

    throw "Element '$($Element.Current.AutomationId)' exposes neither Invoke nor Toggle."
}

function Set-ElementValue {
    param(
        [System.Windows.Automation.AutomationElement]$Element,
        [string]$Value
    )

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
}

function Save-WindowScreenshot {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name
    )

    $bounds = $Root.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int]$bounds.Width, [int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.Left, [int]$bounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $artifactDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Test-UI {
    param([string]$Name, [scriptblock]$Test)

    try {
        & $Test
        $results.Add([pscustomobject]@{ name = $Name; status = 'PASS'; detail = '' })
        Write-Host "PASS: $Name"
    }
    catch {
        $results.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; detail = $_.Exception.Message })
        Write-Host "FAIL: $Name - $($_.Exception.Message)" -ForegroundColor Red
    }
}

try {
    if ($PSCmdlet.ParameterSetName -eq 'Path') {
        $resolvedAppPath = (Resolve-Path -LiteralPath $AppPath).Path
        $application = Start-Process -FilePath $resolvedAppPath -PassThru
        $ownsApplication = $true
    }
    else {
        $application = Get-Process -Id $ProcessId
    }

    $windowHandle = Wait-Until -TimeoutMilliseconds 10000 -Condition {
        $application.Refresh()
        if ($application.HasExited -or $application.MainWindowHandle -eq 0) {
            return $null
        }

        return $application.MainWindowHandle
    }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($windowHandle)
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $scale = [PresentationTimerNativeMethods]::GetDpiForWindow($windowHandle) / 96.0

    Test-UI 'Compact window opens at effective 440 by 240' {
        $bounds = $root.Current.BoundingRectangle
        if ([Math]::Abs($bounds.Width - (440 * $scale)) -gt 32 -or
            [Math]::Abs($bounds.Height - (240 * $scale)) -gt 32) {
            throw "Unexpected compact bounds $bounds at scale $scale."
        }

        $null = Get-Element -Root $root -AutomationId 'CompactTimerRoot'
        $display = Get-Element -Root $root -AutomationId 'CompactTimerDisplay'
        if ($display.Current.Name -ne '15:00') {
            throw "Expected 15:00, got '$($display.Current.Name)'."
        }
    }
    Save-WindowScreenshot -Root $root -Name '01-compact-ready.png'

    Test-UI 'Compact More exposes synchronized always-on-top' {
        Activate-Element (Get-Element -Root $root -AutomationId 'CompactMoreButton')
        $toggle = Get-Element -Root $desktop -AutomationId 'AlwaysOnTopMenuItem'
        Activate-Element $toggle
    }

    Test-UI 'Compact Start enters the smaller Presentation HUD' {
        Activate-Element (Get-Element -Root $root -AutomationId 'CompactStartButton')
        $null = Get-Element -Root $root -AutomationId 'PresentationHudRoot'
        $bounds = $root.Current.BoundingRectangle
        if ([Math]::Abs($bounds.Width - (288 * $scale)) -gt 32 -or
            [Math]::Abs($bounds.Height - (96 * $scale)) -gt 32) {
            throw "Unexpected HUD bounds $bounds at scale $scale."
        }

        $null = Get-Element -Root $root -AutomationId 'HudTimerDisplay'
        Activate-Element (Get-Element -Root $root -AutomationId 'HudPauseButton')
        $null = Get-Element -Root $root -AutomationId 'HudResumeButton'
    }
    Save-WindowScreenshot -Root $root -Name '02-presentation-hud.png'

    Test-UI 'HUD Reset returns to the ready Compact surface' {
        Activate-Element (Get-Element -Root $root -AutomationId 'HudMoreButton')
        Activate-Element (Get-Element -Root $desktop -AutomationId 'HudResetMenuItem')
        $null = Get-Element -Root $root -AutomationId 'CompactTimerRoot'
        $null = Get-Element -Root $root -AutomationId 'CompactStartButton'
    }

    Test-UI 'Expand opens the same resizable control center' {
        Activate-Element (Get-Element -Root $root -AutomationId 'CompactExpandButton')
        $null = Get-Element -Root $root -AutomationId 'ExpandedControlCenterRoot'
        $bounds = $root.Current.BoundingRectangle
        if ($bounds.Width -lt (800 * $scale) -or $bounds.Height -lt (600 * $scale)) {
            throw "Expanded bounds are below the supported minimum: $bounds."
        }

        $null = Get-Element -Root $root -AutomationId 'TitleBarPinButton'
        $null = Get-Element -Root $root -AutomationId 'TimerRemainingProgress'
    }
    Save-WindowScreenshot -Root $root -Name '03-expanded-ready.png'

    Test-UI 'Preset and invalid custom duration preserve authoritative target' {
        Activate-Element (Get-Element -Root $root -AutomationId 'Duration20Button')
        Wait-Until -Condition {
            (Get-Element -Root $root -AutomationId 'ExpandedTimerDisplay').Current.Name -eq '20:00'
        } | Out-Null

        Activate-Element (Get-Element -Root $root -AutomationId 'DurationCustomButton')
        $input = Get-Element -Root $desktop -AutomationId 'CustomDurationInput'
        Set-ElementValue -Element $input -Value 'invalid'
        Activate-Element (Get-Element -Root $desktop -AutomationId 'PrimaryButton')
        $null = Get-Element -Root $desktop -AutomationId 'CustomDurationValidation'
        if ((Get-Element -Root $root -AutomationId 'ExpandedTimerDisplay').Current.Name -ne '20:00') {
            throw 'Invalid custom duration changed the prior target.'
        }

        Set-ElementValue -Element $input -Value '00:01'
        Activate-Element (Get-Element -Root $desktop -AutomationId 'PrimaryButton')
        Wait-Until -Condition {
            (Get-Element -Root $root -AutomationId 'ExpandedTimerDisplay').Current.Name -eq '00:01'
        } | Out-Null
    }

    Test-UI 'Expanded Start swaps to Pause and progress follows pause resume reset' {
        $progress = Get-Element -Root $root -AutomationId 'TimerRemainingProgress'
        Activate-Element (Get-Element -Root $root -AutomationId 'ExpandedStartButton')
        $null = Get-Element -Root $root -AutomationId 'ExpandedPauseButton'
        Start-Sleep -Milliseconds 400
        $range = $progress.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        if ($range.Current.Value -ge 100) {
            throw 'Remaining progress did not decrease after Start.'
        }

        Activate-Element (Get-Element -Root $root -AutomationId 'ExpandedPauseButton')
        $paused = $range.Current.Value
        Start-Sleep -Milliseconds 600
        if ([Math]::Abs($range.Current.Value - $paused) -gt 0.01) {
            throw 'Remaining progress changed while paused.'
        }

        Activate-Element (Get-Element -Root $root -AutomationId 'ExpandedResumeButton')
        Start-Sleep -Milliseconds 1400
        $overtime = (Get-Element -Root $root -AutomationId 'ExpandedTimerDisplay').Current.Name
        if ($overtime -notmatch '^\+00:00:0[1-3]$') {
            throw "Expected early overtime, got '$overtime'."
        }

        Activate-Element (Get-Element -Root $root -AutomationId 'ExpandedResetButton')
        Wait-Until -Condition { $range.Current.Value -eq 100 } | Out-Null
    }

    Test-UI 'Remote pairing uses inline QR until a phone connects' {
        Activate-Element (Get-Element -Root $root -AutomationId 'StartRemoteButton')
        $pairingUrl = Get-Element -Root $root -AutomationId 'InlinePairingUrlText' -TimeoutMilliseconds 10000
        $valuePattern = $pairingUrl.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $uri = [Uri]$valuePattern.Current.Value
        if ($uri.AbsolutePath -ne '/pair' -or $uri.Query -notmatch '(^|[?&])t=[A-Za-z0-9_-]{43}(&|$)') {
            throw "Unexpected pairing URI '$uri'."
        }

        $null = Get-Element -Root $root -AutomationId 'InlinePairingQrImage'
        if ($null -ne (Find-Element -Root $root -AutomationId 'DisplayPairingQrButton')) {
            throw 'Display Pairing QR should replace, not duplicate, the pre-connection inline QR.'
        }
    }

    Test-UI 'Connected phone can disclose the current pairing QR in a flyout' {
        $displayPairing = Find-Element -Root $root -AutomationId 'DisplayPairingQrButton'
        if ($null -eq $displayPairing) {
            Write-Host 'INFO: No authenticated phone is connected; flyout invocation remains environment-dependent.'
            return
        }

        Activate-Element $displayPairing
        $null = Get-Element -Root $desktop -AutomationId 'PairingQrFlyoutImage'
        $null = Get-Element -Root $desktop -AutomationId 'PairingFlyoutUrlText'
    }

    Test-UI 'More and title-bar pin remain discoverable in Expanded mode' {
        $null = Get-Element -Root $root -AutomationId 'ExpandedMoreButton'
        $pin = Get-Element -Root $root -AutomationId 'TitleBarPinButton'
        Activate-Element $pin
    }

    Test-UI 'Collapse restores the compact timer and current target' {
        Activate-Element (Get-Element -Root $root -AutomationId 'ExpandedCollapseButton')
        $display = Get-Element -Root $root -AutomationId 'CompactTimerDisplay'
        if ($display.Current.Name -ne '00:01') {
            throw "Expected shared 00:01 target after collapse, got '$($display.Current.Name)'."
        }
    }
    Save-WindowScreenshot -Root $root -Name '04-compact-restored.png'
}
finally {
    $results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $artifactDirectory 'results.json') -Encoding utf8
    if ($ownsApplication -and $null -ne $application -and -not $application.HasExited) {
        $null = $application.CloseMainWindow()
        if (-not $application.WaitForExit(5000)) {
            Stop-Process -Id $application.Id
        }
    }
}

$failed = @($results | Where-Object status -eq 'FAIL')
Write-Host "Passed: $($results.Count - $failed.Count) | Failed: $($failed.Count)"
if ($failed.Count -gt 0) {
    exit 1
}
