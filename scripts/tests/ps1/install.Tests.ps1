Describe 'Drawplate installer contract' {
    BeforeAll {
        $script:root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:installer = Join-Path $script:root 'scripts/dist/install.ps1'
        $script:source = Join-Path $script:root 'scripts/src/ps1/install.ps1'
        $script:serverScript = Join-Path $script:root 'scripts/tests/serve_release.py'
        $script:temp = Join-Path $env:TEMP ('drawplate-test-' + [Guid]::NewGuid().ToString('N'))
        $script:release = Join-Path $script:temp 'release'
        $script:requests = Join-Path $script:temp 'requests.log'
        $script:originalPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        $script:servers = New-Object System.Collections.ArrayList
        $script:psExe = if ($PSVersionTable.PSEdition -eq 'Core') { (Get-Command pwsh).Source } else { Join-Path $PSHOME 'powershell.exe' }
        New-Item -ItemType Directory -Path $script:temp -Force | Out-Null

        function New-Release([string]$Tag, [string]$Version, [string]$BinaryPath) {
            $folder = Join-Path (Join-Path $script:release 'download') $Tag
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            Copy-Item -LiteralPath $BinaryPath -Destination (Join-Path $folder 'drawplate.exe') -Force
            Copy-Item -LiteralPath (Join-Path $script:root 'LICENSE') -Destination (Join-Path $folder 'LICENSE') -Force
            Copy-Item -LiteralPath (Join-Path $script:root 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $folder 'THIRD-PARTY-NOTICES.md') -Force
            $name = "drawplate-$Version-win-x64.zip"
            $archive = Join-Path $folder $name
            Compress-Archive -Path @((Join-Path $folder 'drawplate.exe'), (Join-Path $folder 'LICENSE'), (Join-Path $folder 'THIRD-PARTY-NOTICES.md')) -DestinationPath $archive
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
            [System.IO.File]::WriteAllText((Join-Path $folder 'SHA256SUMS'), ($hash + '  ' + $name + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
        }

        function Start-ReleaseServer([string]$Fault = 'none') {
            $portFile = Join-Path $script:temp ([Guid]::NewGuid().ToString('N') + '.port')
            $outFile = Join-Path $script:temp ([Guid]::NewGuid().ToString('N') + '.out')
            $errFile = Join-Path $script:temp ([Guid]::NewGuid().ToString('N') + '.err')
            $args = '-u "' + $script:serverScript + '" --root "' + $script:release + '" --port-file "' + $portFile + '" --requests-file "' + $script:requests + '" --fault ' + $Fault
            $process = Start-Process -FilePath 'python' -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $outFile -RedirectStandardError $errFile -WorkingDirectory $script:root
            $count = 0
            while (-not (Test-Path -LiteralPath $portFile) -and $count -lt 100) { Start-Sleep -Milliseconds 100; $count++ }
            if (-not (Test-Path -LiteralPath $portFile)) { throw "Local release server failed: $(Get-Content -LiteralPath $errFile -Raw)" }
            $port = [System.IO.File]::ReadAllText($portFile).Trim()
            $server = @{ Process = $process; BaseUrl = "http://127.0.0.1:$port/releases" }
            [void]$script:servers.Add($server)
            return $server
        }

        function Stop-ReleaseServer($Server) {
            if ($Server -and -not $Server.Process.HasExited) { $Server.Process.Kill(); $Server.Process.WaitForExit() }
        }

        function Request-Count {
            if (-not (Test-Path -LiteralPath $script:requests)) { return 0 }
            return (Get-Content -LiteralPath $script:requests | Measure-Object -Line).Lines
        }

        function Run-Installer($Server, [string]$LocalAppData, [string]$Bin, [string]$Tag = 'v1.2.3', [switch]$Latest, [switch]$Uninstall) {
            New-Item -ItemType Directory -Path $LocalAppData -Force | Out-Null
            $args = '-NoProfile -ExecutionPolicy Bypass -File "' + $script:installer + '" -Dir "' + $Bin + '" -NoSetup'
            if ($Tag -and -not $Latest) { $args += ' -Version ' + $Tag }
            if ($Uninstall) { $args += ' -Uninstall' }
            $start = [System.Diagnostics.ProcessStartInfo]::new()
            $start.FileName = $script:psExe
            $start.Arguments = $args
            $start.WorkingDirectory = $script:root
            $start.UseShellExecute = $false
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $start.EnvironmentVariables['LOCALAPPDATA'] = $LocalAppData
            $start.EnvironmentVariables['DRAWPLATE_BASE_URL'] = $Server.BaseUrl
            $start.EnvironmentVariables['DRAWPLATE_VERSION'] = if ($Latest) { '' } else { $Tag }
            $start.EnvironmentVariables['DRAWPLATE_INSTALL_DIR'] = $Bin
            $start.EnvironmentVariables['DRAWPLATE_NO_SETUP'] = '1'
            $process = [System.Diagnostics.Process]::new()
            $process.StartInfo = $start
            [void]$process.Start()
            $stdout = $process.StandardOutput.ReadToEnd()
            $stderr = $process.StandardError.ReadToEnd()
            $process.WaitForExit()
            return @{ Code = $process.ExitCode; Out = $stdout; Err = $stderr }
        }

        New-Release 'v1.2.3' '1.2.3' (Join-Path $script:root 'out/drawplate.exe')
        $script:server = Start-ReleaseServer
        $script:localAppData = Join-Path $script:temp 'home'
        $script:bin = Join-Path $script:localAppData 'Programs/Drawplate'
        $script:first = Run-Installer $script:server $script:localAppData $script:bin
    }

    AfterAll {
        foreach ($server in $script:servers) { Stop-ReleaseServer $server }
        [Environment]::SetEnvironmentVariable('Path', $script:originalPath, 'User')
        if (Test-Path -LiteralPath $script:temp) { Remove-Item -LiteralPath $script:temp -Recurse -Force }
    }

    It 'T-INS-01 installs from a local release' {
        $script:first.Code | Should -Be 0
        (Test-Path -LiteralPath (Join-Path $script:bin 'drawplate.exe')) | Should -BeTrue
        (Test-Path -LiteralPath (Join-Path $script:localAppData 'Drawplate/install.json')) | Should -BeTrue
        $script:first.Out | Should -Match 'Drawplate installer 1.2.3'
        $script:first.Out | Should -Match 'Your user PATH was updated'
    }

    It 'T-INS-02 skips an up-to-date installation' {
        $before = Request-Count
        $result = Run-Installer $script:server $script:localAppData $script:bin
        $result.Code | Should -Be 0
        (Request-Count) | Should -Be $before
        $result.Out | Should -Match 'up to date'
    }

    It 'T-INS-03 repairs a corrupted installed binary' {
        $binary = Join-Path $script:bin 'drawplate.exe'
        [System.IO.File]::WriteAllText($binary, 'truncated')
        $result = Run-Installer $script:server $script:localAppData $script:bin
        $result.Code | Should -Be 0
        $result.Out | Should -Match 'repairing'
        $manifest = Get-Content -LiteralPath (Join-Path $script:localAppData 'Drawplate/install.json') -Raw | ConvertFrom-Json
        (Get-FileHash -Algorithm SHA256 -LiteralPath $binary).Hash.ToLowerInvariant() | Should -Be $manifest.binarySha256
    }

    It 'T-INS-04 rejects a bad checksum' {
        $server = Start-ReleaseServer 'badsums'
        $localAppData = Join-Path $script:temp 'home-badsums'
        $bin = Join-Path $localAppData 'Programs/Drawplate'
        $result = Run-Installer $server $localAppData $bin
        $result.Code | Should -Be 13
        $result.Err | Should -Match 'Checksum mismatch'
        (Test-Path -LiteralPath (Join-Path $bin 'drawplate.exe')) | Should -BeFalse
    }

    It 'T-INS-05 rejects a truncated download and permits retry' {
        $server = Start-ReleaseServer 'truncate'
        $localAppData = Join-Path $script:temp 'home-truncate'
        $bin = Join-Path $localAppData 'Programs/Drawplate'
        $failed = Run-Installer $server $localAppData $bin
        $failed.Code | Should -BeIn @(12, 13)
        (Test-Path -LiteralPath (Join-Path $bin 'drawplate.exe')) | Should -BeFalse
        Stop-ReleaseServer $server
        (Run-Installer $script:server $localAppData $bin).Code | Should -Be 0
    }

    It 'T-INS-06 restores the previous binary when a new executable is invalid' {
        $folder = Join-Path (Join-Path $script:release 'download') 'v1.2.4'
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $folder 'drawplate.exe'), 'not an executable')
        Copy-Item -LiteralPath (Join-Path $script:root 'LICENSE') -Destination (Join-Path $folder 'LICENSE')
        Copy-Item -LiteralPath (Join-Path $script:root 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $folder 'THIRD-PARTY-NOTICES.md')
        $name = 'drawplate-1.2.4-win-x64.zip'
        $archive = Join-Path $folder $name
        Compress-Archive -Path @((Join-Path $folder 'drawplate.exe'), (Join-Path $folder 'LICENSE'), (Join-Path $folder 'THIRD-PARTY-NOTICES.md')) -DestinationPath $archive
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
        [System.IO.File]::WriteAllText((Join-Path $folder 'SHA256SUMS'), ($hash + '  ' + $name + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
        $before = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $script:bin 'drawplate.exe')).Hash
        $result = Run-Installer $script:server $script:localAppData $script:bin 'v1.2.4'
        $result.Code | Should -Be 14
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $script:bin 'drawplate.exe')).Hash | Should -Be $before
    }

    It 'T-INS-07 rejects an active lock folder' {
        $lock = Join-Path (Join-Path $script:localAppData 'Drawplate') '.lock'
        New-Item -ItemType Directory -Path $lock -Force | Out-Null
        $result = Run-Installer $script:server $script:localAppData $script:bin
        $result.Code | Should -Be 15
        $result.Err | Should -Match 'Another installation is running'
        Remove-Item -LiteralPath $lock -Recurse -Force
    }

    It 'T-INS-08 writes plain redirected progress output' {
        $script:first.Out | Should -Match '25%'
        $script:first.Out | Should -Match '50%'
        $script:first.Out | Should -Match '75%'
        $script:first.Out | Should -Match '100%'
        $script:first.Out | Should -Not -Match '\r|\x1b'
        [System.Text.Encoding]::ASCII.GetString([System.Text.Encoding]::ASCII.GetBytes($script:first.Out)) | Should -Be $script:first.Out
    }

    It 'T-INS-09 uninstalls without touching FFmpeg' {
        $result = Run-Installer $script:server $script:localAppData $script:bin -Uninstall
        $result.Code | Should -Be 0
        (Test-Path -LiteralPath (Join-Path $script:bin 'drawplate.exe')) | Should -BeFalse
        (Test-Path -LiteralPath (Join-Path $script:localAppData 'Drawplate/install.json')) | Should -BeFalse
        $result.Out | Should -Match 'FFmpeg was not touched'
    }

    It 'T-INS-10 runs in Windows PowerShell 5.1 and PowerShell 7+' {
        $PSVersionTable.PSVersion.Major | Should -BeIn @(5, 7)
    }

    It 'T-INS-11 keeps source ASCII-only and avoids unsupported syntax' {
        $bytes = [System.IO.File]::ReadAllBytes($script:source)
        ($bytes | Where-Object { $_ -gt 127 }).Count | Should -Be 0
        $text = [System.Text.Encoding]::ASCII.GetString($bytes)
        $text | Should -Not -Match '\?\:|\?\?|\?\.|\?\[|&&|\|\||\bInvoke-WebRequest\b|\$IsWindows|\$IsLinux|\$IsMacOS|\-AsHashtable|\-SkipHttpErrorCheck'
    }

    It 'T-INS-12 resolves latest from the HEAD redirect' {
        $localAppData = Join-Path $script:temp 'home-latest'
        $bin = Join-Path $localAppData 'Programs/Drawplate'
        $result = Run-Installer $script:server $localAppData $bin -Latest
        $result.Code | Should -Be 0
        $result.Out | Should -Match 'Finding release.*v1.2.3'
    }
}
