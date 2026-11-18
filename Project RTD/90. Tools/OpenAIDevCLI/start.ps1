$ErrorActionPreference = 'Stop'

$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$entryPoint = Join-Path $toolRoot 'api-dev.mjs'

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js를 찾지 못했습니다. 새 PowerShell 창을 연 뒤 다시 실행하세요.'
}

$forwardArgs = @()
$cliMode = $false
foreach ($argument in $args) {
    if ($argument -eq '--cli') {
        $cliMode = $true
    } else {
        $forwardArgs += $argument
    }
}

if (-not $cliMode -and -not ($forwardArgs -contains '--web') -and -not ($forwardArgs -contains '--prompt')) {
    $forwardArgs = @('--web') + $forwardArgs
}

& node $entryPoint @forwardArgs
exit $LASTEXITCODE
