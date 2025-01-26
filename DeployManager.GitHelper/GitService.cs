using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeployManager.GitHelper;

public class GitService
{
    private string _repoPath = string.Empty;
    private string _mainBranch = "develop";

    public event EventHandler<GitCommandEventArgs>? GitCommandStarted;
    public event EventHandler<GitCommandEventArgs>? GitCommandFinished;

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

    public async Task<Status> GetStatus(string environment)
    {
        await RunGitCommandAsync("fetch --all");

        var logOutput = await RunGitCommandAsync($"log {environment}..{_mainBranch} --pretty=format:%H|%ad|%an|%s --date=iso8601");

        var currentCommitLog = await RunGitCommandAsync($"log -1 {environment} --pretty=format:%H|%ad|%an|%s --date=iso8601");

        var pendingCommits = logOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Commit.ParseCommit(line))
            .ToList();

        var currentCommit = string.IsNullOrWhiteSpace(currentCommitLog)
            ? null
            : Commit.ParseCommit(currentCommitLog);

        return new Status
        {
            CurrentCommit = currentCommit,
            PendingCommits = pendingCommits
        };

    }

    public async Task<string> Deploy(string environment)
    {
        await RunGitCommandAsync("fetch --all");
        await RunGitCommandAsync($"tag -d {environment}");
        await RunGitCommandAsync($"push origin :refs/tags/{environment}");
        await RunGitCommandAsync($"tag {environment} {_mainBranch}");
        await RunGitCommandAsync($"push origin {environment}");

        // Get the SHA of the head of the main branch
        var sha = await RunGitCommandAsync($"rev-parse {_mainBranch}");
        return "Current sha: " + sha;
    }

    private async Task<string> RunGitCommandAsync(string arguments)
    {
        OnGitCommandStarted(arguments);

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

        OnGitCommandFinished(arguments, process.ExitCode);

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new Exception($"Git command failed: {error}");
        }

        return output;
    }

    protected virtual void OnGitCommandStarted(string arguments)
    {
        GitCommandStarted?.Invoke(this, new GitCommandEventArgs(arguments, null));
    }

    protected virtual void OnGitCommandFinished(string arguments, int? exitCode)
    {
        GitCommandFinished?.Invoke(this, new GitCommandEventArgs(arguments, exitCode));
    }
}

public class GitCommandEventArgs : EventArgs
{
    public string Arguments { get; }
    public int? ExitCode { get; }

    public GitCommandEventArgs(string arguments, int? exitCode)
    {
        Arguments = arguments;
        ExitCode = exitCode;
    }
}

public class Commit
{
    public string Hash { get; set; }
    public DateTime Date { get; set; }
    public string Author { get; set; }
    public string Message { get; set; }

    public static Commit ParseCommit(string logLine)
    {
        var parts = logLine.Split('|');
        var commit = new Commit
        {
            Hash = parts[0],
            Date = DateTime.Parse(parts[1]),
            Author = parts[2],
            Message = parts[3]
        };

        // Check if the message matches the pattern and add the link if it does
        var match = Regex.Match(commit.Message, @"Merge pull request #(\d+) from drdk/feature/distributed-cache");
        if (!match.Success) return commit;

        var pullRequestNumber = match.Groups[1].Value;
        commit.Message =  $"<a href='https://github.com/drdk/umbraco-artikel-cms/pull/{pullRequestNumber}'>{commit.Message}</a>";

        return commit;
    }
}

public class Status
{
    public Commit? CurrentCommit { get; set; }
    public List<Commit> PendingCommits { get; set; } = new();
}
