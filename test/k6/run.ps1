param(
  [ValidateSet('steady','step')]
  [string]$Mode = 'steady',
  [int]$RPS = 200,
  [string]$Duration = '60s',
  [int]$PreVUs = 50,
  [int]$MaxVUs = 500,
  [string]$BaseUrl = 'http://localhost:5145',
  [int]$Accounts = 100,
  [int]$InitBalance = 100000,
  [int]$Amount = 1,
  # step mode only
  [int]$StartRps = 100,
  [int]$StepRps = 100,
  [int]$Steps = 6,
  [string]$StageDuration = '30s',
  [switch]$Insecure
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

# 确保安装 k6：https://k6.io/docs/get-started/installation/
$k6 = Get-Command k6 -ErrorAction SilentlyContinue
if (-not $k6) {
  Write-Error '未检测到 k6，请先安装 k6 并将其加入 PATH。参考：https://k6.io/docs/get-started/installation/'
  exit 1
}

$insecureFlag = ''
if ($Insecure -or $BaseUrl.StartsWith('https://')) { $insecureFlag = '--insecure-skip-tls-verify' }

if ($Mode -eq 'steady') {
  & k6 run $insecureFlag `
    -e BASE_URL=$BaseUrl `
    -e ACCOUNTS=$Accounts `
    -e INIT_BALANCE=$InitBalance `
    -e AMOUNT=$Amount `
    -e RPS=$RPS `
    -e DURATION=$Duration `
    -e PRE_VUS=$PreVUs `
    -e MAX_VUS=$MaxVUs `
    ./steady-rps.js
}
elseif ($Mode -eq 'step') {
  & k6 run $insecureFlag `
    -e BASE_URL=$BaseUrl `
    -e ACCOUNTS=$Accounts `
    -e INIT_BALANCE=$InitBalance `
    -e AMOUNT=$Amount `
    -e START_RPS=$StartRps `
    -e STEP_RPS=$StepRps `
    -e STEPS=$Steps `
    -e STAGE_DURATION=$StageDuration `
    -e PRE_VUS=$PreVUs `
    -e MAX_VUS=$MaxVUs `
    ./step-rps.js
}
else {
  Write-Error "未知的 Mode: $Mode"
  exit 1
}

