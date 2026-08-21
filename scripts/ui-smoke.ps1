param(
    [Parameter(Mandatory)]
    [string]$AppPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$artifactDirectory = Join-Path $PSScriptRoot '..\artifacts\ui-smoke'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
$application = $null

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

function Get-Element {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)]
        [string]$AutomationId,
        [int]$TimeoutMilliseconds = 5000
    )

    return Wait-Until -TimeoutMilliseconds $TimeoutMilliseconds -Condition {
        $condition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            $AutomationId)
        $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
}

function Invoke-Element {
    param([System.Windows.Automation.AutomationElement]$Element)
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
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
        $graphics.CopyFromScreen(
            [int]$bounds.Left,
            [int]$bounds.Top,
            0,
            0,
            $bitmap.Size)
        $bitmap.Save((Join-Path $artifactDirectory $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Test-UI {
    param(
        [string]$Name,
        [scriptblock]$Test
    )

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
    $resolvedAppPath = (Resolve-Path -LiteralPath $AppPath).Path
    $application = Start-Process -FilePath $resolvedAppPath -PassThru
    $windowHandle = Wait-Until -TimeoutMilliseconds 10000 -Condition {
        $application.Refresh()
        if ($application.HasExited -or $application.MainWindowHandle -eq 0) {
            return $null
        }

        return $application.MainWindowHandle
    }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($windowHandle)

    Test-UI 'Top-level window is visible' {
        if ($root.Current.BoundingRectangle.Width -lt 800 -or $root.Current.BoundingRectangle.Height -lt 600) {
            throw "Unexpected window bounds $($root.Current.BoundingRectangle)."
        }
    }

    Test-UI 'Core interactive controls are discoverable' {
        foreach ($id in @(
            'AlwaysOnTopToggle',
            'DurationInput',
            'StartButton',
            'PauseButton',
            'ResumeButton',
            'ResetButton')) {
            $null = Get-Element -Root $root -AutomationId $id
        }
    }

    Test-UI 'Initial fifteen-minute display is ready' {
        $display = Get-Element -Root $root -AutomationId 'CountdownDisplay'
        if ($display.Current.Name -ne '15:00') {
            throw "Expected 15:00, got '$($display.Current.Name)'."
        }

        $start = Get-Element -Root $root -AutomationId 'StartButton'
        if (-not $start.Current.IsEnabled) {
            throw 'Start button should be enabled.'
        }
    }
    Save-WindowScreenshot -Root $root -Name '01-initial.png'

    Test-UI 'Always-on-top toggle changes state' {
        $toggle = Get-Element -Root $root -AutomationId 'AlwaysOnTopToggle'
        $pattern = $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $pattern.Toggle()
        Wait-Until -Condition {
            $pattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On
        } | Out-Null
    }

    Test-UI 'One-second timer starts and reaches overtime' {
        Set-ElementValue -Element (Get-Element -Root $root -AutomationId 'DurationInput') -Value '00:01'
        Invoke-Element -Element (Get-Element -Root $root -AutomationId 'StartButton')
        Wait-Until -Condition {
            (Get-Element -Root $root -AutomationId 'PauseButton').Current.IsEnabled
        } | Out-Null
        Start-Sleep -Milliseconds 2200
        $overtime = Get-Element -Root $root -AutomationId 'OvertimeDisplay'
        if ($overtime.Current.Name -notmatch '^00:0[1-3]$') {
            throw "Expected early overtime, got '$($overtime.Current.Name)'."
        }
    }
    Save-WindowScreenshot -Root $root -Name '02-overtime.png'

    Test-UI 'Pause freezes overtime and resume continues' {
        Invoke-Element -Element (Get-Element -Root $root -AutomationId 'PauseButton')
        $paused = (Get-Element -Root $root -AutomationId 'OvertimeDisplay').Current.Name
        Start-Sleep -Milliseconds 1200
        $stillPaused = (Get-Element -Root $root -AutomationId 'OvertimeDisplay').Current.Name
        if ($paused -ne $stillPaused) {
            throw "Overtime changed while paused: '$paused' to '$stillPaused'."
        }

        Invoke-Element -Element (Get-Element -Root $root -AutomationId 'ResumeButton')
        Start-Sleep -Milliseconds 1200
        $resumed = (Get-Element -Root $root -AutomationId 'OvertimeDisplay').Current.Name
        if ($resumed -eq $paused) {
            throw "Overtime did not continue after resume; value stayed '$resumed'."
        }
    }

    Test-UI 'Reset restores configured target and ready controls' {
        Invoke-Element -Element (Get-Element -Root $root -AutomationId 'ResetButton')
        $display = Get-Element -Root $root -AutomationId 'CountdownDisplay'
        if ($display.Current.Name -ne '00:01') {
            throw "Expected reset value 00:01, got '$($display.Current.Name)'."
        }

        if (-not (Get-Element -Root $root -AutomationId 'StartButton').Current.IsEnabled) {
            throw 'Start button should be enabled after reset.'
        }
    }
    Save-WindowScreenshot -Root $root -Name '03-reset.png'

    Test-UI 'Remote session shows an exact token-bearing URL and QR' {
        Invoke-Element -Element (Get-Element -Root $root -AutomationId 'StartRemoteButton')
        $pairingUrl = Get-Element -Root $root -AutomationId 'PairingUrlText' -TimeoutMilliseconds 10000
        $valuePattern = $pairingUrl.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $uri = [Uri]$valuePattern.Current.Value
        if ($uri.AbsolutePath -ne '/pair' -or $uri.Query -notmatch '(^|[?&])t=[A-Za-z0-9_-]{43}(&|$)') {
            throw "Unexpected pairing URI '$uri'."
        }

        $null = Get-Element -Root $root -AutomationId 'PairingQrImage'
        if (-not (Get-Element -Root $root -AutomationId 'EndRemoteButton').Current.IsEnabled) {
            throw 'End session should be enabled while pairing is available.'
        }
    }
    Save-WindowScreenshot -Root $root -Name '04-remote.png'

    Test-UI 'Ending remote session clears pairing material immediately' {
        Invoke-Element -Element (Get-Element -Root $root -AutomationId 'EndRemoteButton')
        Wait-Until -TimeoutMilliseconds 10000 -Condition {
            (Get-Element -Root $root -AutomationId 'StartRemoteButton').Current.IsEnabled
        } | Out-Null
        $pairing = $root.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                'PairingUrlText'))
        if ($null -ne $pairing) {
            throw 'Pairing URL remained in the automation tree after session end.'
        }
    }

    Test-UI 'Interactive controls expose stable AutomationIds' {
        $interactiveTypes = @(
            [System.Windows.Automation.ControlType]::Button,
            [System.Windows.Automation.ControlType]::Edit,
            [System.Windows.Automation.ControlType]::CheckBox)
        $elements = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        $missing = @()
        foreach ($element in $elements) {
            if ($interactiveTypes -contains $element.Current.ControlType -and
                $element.Current.IsKeyboardFocusable -and
                [string]::IsNullOrWhiteSpace($element.Current.AutomationId)) {
                $missing += $element.Current.Name
            }
        }

        if ($missing.Count -gt 0) {
            throw "Missing AutomationId: $($missing -join ', ')"
        }
    }
}
finally {
    $results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $artifactDirectory 'results.json') -Encoding utf8
    if ($null -ne $application -and -not $application.HasExited) {
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
