using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DeployManager.GitHelper;

public class GitService
{
    public event EventHandler<GitCommandEventArgs>? GitCommandStarted;
    public event EventHandler<GitCommandEventArgs>? GitCommandFinished;

    public string? RepoPath { get; set; }
    public string? Branch { get; set; }
    public string? Environment { get; set; }

    public async Task Prepare()
    {
        if (string.IsNullOrWhiteSpace(RepoPath))
            throw new Exception("Repo path not set.");

        if (!Directory.Exists(RepoPath))
            throw new Exception($"Repo path does not exist: {RepoPath}");

        var gitFolder = Path.Combine(RepoPath, ".git");
        if (!Directory.Exists(gitFolder))
            throw new Exception("Not a valid Git repository.");

        // Fetch all branches and tags
        await RunGitCommandAsync("fetch --all");
        await RunGitCommandAsync("fetch --tags --force");

        // Check if local branch exists
        var localBranches = (await RunGitCommandAsync("branch --list")).Split("\n\r ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        if (!localBranches.Contains(Branch))
        {
            // Checkout new branch tracking remote
            await RunGitCommandAsync($"checkout -b {Branch} origin/{Branch}");
        }
        else
        {
            // Switch to existing branch
            await RunGitCommandAsync($"checkout {Branch}");
        }

        // Pull latest changes
        await RunGitCommandAsync("pull");
    }

    public async Task<Status> GetStatus()
    {
        var logOutput = await RunGitCommandAsync($"log {Environment}..{Branch} --pretty=format:%H|%ad|%an|%s --date=iso8601");

        var currentCommitLog = await RunGitCommandAsync($"log -1 {Environment} --pretty=format:%H|%ad|%an|%s --date=iso8601");

        var pendingCommits = logOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(Commit.ParseCommit)
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

    public async Task<string> Deploy()
    {
        await RunGitCommandAsync($"tag -d {Environment}");
        await RunGitCommandAsync($"push origin :refs/tags/{Environment}");
        await RunGitCommandAsync($"tag {Environment} {Branch}");
        await RunGitCommandAsync($"push origin {Environment}");

        // Get the SHA of the head of the main branch
        var sha = await RunGitCommandAsync($"rev-parse {Branch}");
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
            WorkingDirectory = RepoPath
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

public class GitCommandEventArgs(string arguments, int? exitCode) : EventArgs
{
    public string Arguments { get; } = arguments;
    public int? ExitCode { get; } = exitCode;
}

public class Commit
{
    private Commit(){}

    public required string Hash { get; set; }
    public DateTime Date { get; set; }
    public required string Author { get; set; }
    public required string Message { get; set; }

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
        var match = Regex.Match(commit.Message, @"Merge pull request #(\d+) (.*)");
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
