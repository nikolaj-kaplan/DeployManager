# Variables
$timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
$outputPath = "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish"
$releaseFolder = "release"
$zipFileName = "release_$timestamp.zip"

# Ensure release folder exists
if (-not (Test-Path $releaseFolder)) {
    New-Item -ItemType Directory -Path $releaseFolder | Out-Null
}

# Publish the project
dotnet publish -c Release -f net9.0-windows10.0.19041.0 -r win-x64 --self-contained

# Zip the publish output
if (Test-Path $outputPath) {
    Compress-Archive -Path "$outputPath\*" -DestinationPath "$releaseFolder\$zipFileName"
    Write-Host "Project published and zipped to $releaseFolder\$zipFileName"
} else {
    Write-Host "Publish output path not found: $outputPath"
}
