using System.Diagnostics;

public static class GitHelper
{
    // 1. GetStatus
    public static async Task<List<string>> GetStatus(string environment)
    {
        // Ensure local repo is up-to-date
        await RunGitCommandAsync("fetch --all");

        // Collect log entries between the environment tag and main
        // (If the tag doesn't exist yet, you might want to handle that case separately)
        var logOutput = await RunGitCommandAsync(
            $"log {environment}..main --pretty=format:\"%C(auto)%h | %ad | %an | %s\" --date=short"
        );

        // Split by newline to get each commit as a separate line
        var commits = new List<string>(logOutput.Split(
            new[] { Environment.NewLine },
            StringSplitOptions.RemoveEmptyEntries
        ));

        return commits;
    }

    // 2. Deploy
    public static async Task Deploy(string environment)
    {
        // Ensure local repo is up-to-date
        await RunGitCommandAsync("fetch --all");

        // Delete the old tag locally (ignore errors if tag doesn't exist)
        await RunGitCommandAsync($"tag -d {environment}");

        // Delete the old tag remotely
        // (If you don't want to force-delete if the tag doesn't exist, add error handling)
        await RunGitCommandAsync($"push origin :refs/tags/{environment}");

        // Create new tag at head of main
        await RunGitCommandAsync($"tag {environment} main");

        // Push the new tag
        await RunGitCommandAsync($"push origin {environment}");
    }

    // Helper method to run a Git command and return its output
    private static async Task<string> RunGitCommandAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using (var process = new Process { StartInfo = psi })
        {
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                throw new Exception($"Git command failed: {error}");
            }

            return output;
        }
    }
}
