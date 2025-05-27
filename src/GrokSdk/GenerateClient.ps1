# Define paths and parameters
$swaggerFile = "grok-api.yaml"
$outputFile = "GrokClient.cs"
$namespace = "GrokSdk"
$className = "GrokClient"
$exceptionClassName = "GrokSdkException"
$projectDir = Get-Location

# Ensure nswag CLI is available
if (-not (Get-Command "nswag" -ErrorAction SilentlyContinue)) {
    Write-Error "nswag CLI is not installed or not in PATH. Install it with 'dotnet tool install -g NSwag.ConsoleCore' and ensure it's in your PATH."
    exit 1
}

# Check if swagger file exists
if (-not (Test-Path $swaggerFile)) {
    Write-Error "Swagger file ($swaggerFile) not found in $projectDir. Please ensure grok-api.yaml exists."
    exit 1
}

# Build the nswag command
$nswagCommand = "nswag openapi2csclient " +
                "/input:$swaggerFile " +
                "/namespace:$namespace " +
                "/className:$className " +
                "/exceptionClass:$exceptionClassName " +
                "/output:$outputFile "

# Output detailed information
Write-Host "Generating Grok client from Swagger schema using NSwag CLI..." -ForegroundColor Cyan
Write-Host "Project Directory: $projectDir" -ForegroundColor Yellow
Write-Host "Swagger Input File: $swaggerFile" -ForegroundColor Yellow
Write-Host "Output File: $outputFile" -ForegroundColor Yellow
Write-Host "Namespace: $namespace" -ForegroundColor Yellow
Write-Host "Class Name: $className" -ForegroundColor Yellow
Write-Host "Full NSwag Command: $nswagCommand" -ForegroundColor Magenta
Write-Host "Running NSwag code generation..." -ForegroundColor Cyan

# Execute the NSwag command and capture output
try {
    $output = Invoke-Expression $nswagCommand 2>&1
    Write-Host "NSwag Output:" -ForegroundColor Cyan
    $output | ForEach-Object { Write-Host $_ -ForegroundColor White }

    # Check if the output file was created
    if (Test-Path $outputFile) {
        Write-Host "Success: Generated $outputFile" -ForegroundColor Green
        Write-Host "File Path: $projectDir\$outputFile" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate $outputFile. No output file detected."
        Write-Host "Review the NSwag output above for errors." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Error "Exception occurred during NSwag execution: $_"
    Write-Host "Review the NSwag command and output above for details." -ForegroundColor Red
    exit 1
}

# Post-process the generated file to replace error messages
Write-Host "Post-processing $outputFile to update error messages..." -ForegroundColor Cyan
try {
    # Read the generated file content
    $fileContent = Get-Content -Path $outputFile -Raw

    # Define the pattern to match throw statements with any hardcoded message
    $pattern = '(throw new GrokSdkException<GrokErrorResponse>\()"(.*?)"(,\s*status_,\s*objectResponse_\.Text,\s*headers_,\s*objectResponse_\.Object,\s*null\);)'

    # Define the replacement to use the dynamic error message with the original as fallback
    $replacementPattern = '${1}(objectResponse_.Object.Error ?? "${2}")${3}'

    # Perform the replacement
    $modifiedContent = $fileContent -replace $pattern, $replacementPattern

    # Write the modified content back to the file
    Set-Content -Path $outputFile -Value $modifiedContent -Encoding UTF8

    Write-Host "Success: Updated error messages in $outputFile" -ForegroundColor Green
} catch {
    $exception = $_
    Write-Error "Failed to post-process ${outputFile}: $exception"
    Write-Host "The generated file remains unchanged." -ForegroundColor Red
}

Write-Host "Generation and post-processing completed." -ForegroundColor Cyan