using System.Diagnostics;

namespace DeployManager.GitHelper;

public interface IGitService
{
    void Configure(string repoPath, string mainBranch = "develop");
    Task Prepare();
    Task<List<string>> GetStatus(string environment);
    Task Deploy(string environment);
}

public class GitService : IGitService
{
    private string _repoPath = string.Empty;
    private string _mainBranch = "develop";

    public void Configure(string repoPath, string mainBranch = "develop")
    {
        _repoPath = repoPath;
        _mainBranch = mainBranch;
    }

    public async Task Prepare()
    {
        if (string.IsNullOrWhiteSpace(_repoPath))
            throw new Exception("Repo path not set.");

        if (!Directory.Exists(_repoPath))
            throw new Exception($"Repo path does not exist: {_repoPath}");

        var gitFolder = Path.Combine(_repoPath, ".git");
        if (!Directory.Exists(gitFolder))
            throw new Exception("Not a valid Git repository.");

        await RunGitCommandAsync("fetch");
    }

    public async Task<List<string>> GetStatus(string environment)
    {
        await RunGitCommandAsync("fetch --all");
        var logOutput = await RunGitCommandAsync(
            $"log {environment}..{_mainBranch} --pretty=format:\"%C(auto)%h | %ad | %an | %s\" --date=short"
        );
        var commits = logOutput
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return commits;
    }

    public async Task Deploy(string environment)
    {
        await RunGitCommandAsync("fetch --all");
        await RunGitCommandAsync($"tag -d {environment}");
        await RunGitCommandAsync($"push origin :refs/tags/{environment}");
        await RunGitCommandAsync($"tag {environment} {_mainBranch}");
        await RunGitCommandAsync($"push origin {environment}");
    }

    private async Task<string> RunGitCommandAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = _repoPath
        };

        using var process = new Process();
        process.StartInfo = psi;
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new Exception($"Git command failed: {error}");
        }

        return output;
    }
}