```powershell
$ErrorActionPreference = "Stop"

# Project paths
$root = "D:\Swapnil\Projects\the-sweet-panda-release1\the-sweet-panda-release1"
$apiProject = Join-Path $root "src\CakeShop.Api"
$publishDir = Join-Path $root "publish-api"
$zipPath = Join-Path $root "cake-shop-api.zip"

# Existing Azure resources
$resourceGroup = "rg-sweetpanda-dev"
$appName = "swa-sweetpanda-api-dev"

Set-Location $root

Write-Host "1. Publishing API..." -ForegroundColor Cyan

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $apiProject `
    --configuration Release `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "API publish failed. Deployment cancelled."
}

Write-Host "2. Creating deployment ZIP..." -ForegroundColor Cyan

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive `
    -Path "$publishDir\*" `
    -DestinationPath $zipPath `
    -CompressionLevel Optimal

Write-Host "3. Checking Azure login..." -ForegroundColor Cyan
az account show --output table

if ($LASTEXITCODE -ne 0) {
    throw "Azure CLI login unavailable. Run az login and retry."
}

Write-Host "4. Deploying API ZIP..." -ForegroundColor Cyan

az webapp deploy `
    --resource-group $resourceGroup `
    --name $appName `
    --src-path $zipPath `
    --type zip

if ($LASTEXITCODE -ne 0) {
    throw "Azure deployment failed."
}

Write-Host "5. Checking API health endpoint..." -ForegroundColor Cyan

Start-Sleep -Seconds 10

$healthUrl = "https://swa-sweetpanda-api-dev-e9h7edcsekeqesf6.westus3-01.azurewebsites.net/health"

try {
    $response = Invoke-WebRequest -Uri $healthUrl -TimeoutSec 30
    Write-Host "Health endpoint HTTP status: $([int]$response.StatusCode)" -ForegroundColor Green
    Write-Host $response.Content
}
catch {
    Write-Warning "Deployment ran, but health check failed. Inspect App Service logs before retrying."
    Write-Warning $_.Exception.Message
}

Write-Host "ZIP location: $zipPath"
Write-Host "API URL: https://swa-sweetpanda-api-dev-e9h7edcsekeqesf6.westus3-01.azurewebsites.net"
```