Describe 'Installer progress output contract' {
    It 'keeps the distribution ASCII-only and free of terminal escape sequences' {
        $path = Join-Path $PSScriptRoot '../../dist/install.ps1'
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $path))
        ($bytes | Where-Object { $_ -gt 127 }).Count | Should -Be 0
        $text = [System.Text.Encoding]::ASCII.GetString($bytes)
        $text | Should -Not -Match '\x1b'
        $text | Should -Not -Match '\r'
    }
}
