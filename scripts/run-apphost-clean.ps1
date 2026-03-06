$ErrorActionPreference = 'Stop'

Write-Host 'Stopping stale Taskify/Aspire processes...'
$targets = Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'dotnet.exe' -and (
            $_.CommandLine -like '*Taskify.AppHost*' -or
            $_.CommandLine -like '*Taskify.Api.csproj*' -or
            $_.CommandLine -like '*Taskify.Web.csproj*' -or
            $_.CommandLine -like '*Taskify.Migrator.csproj*' -or
            $_.CommandLine -like '*Aspire.Dashboard.dll*'
        )
    }

foreach ($proc in $targets) {
    Write-Host (" - stopping PID {0}" -f $proc.ProcessId)
    Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
}

Write-Host 'Starting AppHost (no-build)...'
dotnet run --project "src/Taskify.AppHost/Taskify.AppHost.csproj" --no-build