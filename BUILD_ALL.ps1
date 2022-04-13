$projectNames = @(
    "CaptureWindow",
    "CaptureWindowToPng",
    "CaptureWindowClientAreaToPng");
$configNames = @("Debug", "Release");
$targetNames = @("net48")

./BUILD_CLEAN.ps1

foreach ($project in $projectNames) {
    foreach ($config in $configNames) {
        dotnet build -c $config $project;
        foreach ($target in $targetNames) {
            $path = "bin/$config/$target";
            mkdir -Force -Path $path > $null;
            Copy-Item `
                -Recurse -Force `
                -Path $project/$path/* `
                -Destination $path;
        }
    }
}
