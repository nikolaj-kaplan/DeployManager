using DeployManager.GitHelper;
using System.Runtime.CompilerServices;
using System.Text;

namespace DeployManager.UI
{
    public partial class MainPage
    {
        private readonly GitService _gitService;
        private ConfigService _configService;

        public MainPage(GitService gitService, ConfigService configService)
        {
            InitializeComponent();
            _gitService = gitService;
            _configService = configService;


            _gitService.GitCommandStarted += (sender, args) =>
            {
                StatusEditor.Text += "\n>git " + args.Arguments + "...";
                ActivityIndicator.IsRunning = true;
                ActivityIndicator.IsVisible = true;
            };
            _gitService.GitCommandFinished += (sender, args) =>
            {
                StatusEditor.Text += " " + (args.ExitCode == 0 ? "ok." : "error.");
                ActivityIndicator.IsRunning = false;
                ActivityIndicator.IsVisible = false;
            };
            // Load configuration on startup
            LoadConfigurationAsync();

            // Disable buttons if invalid configuration
            _ = ValidateInputsAsync();
        }

        private async void LoadConfigurationAsync()
        {
            _configService = ConfigService.LoadConfig();


            RepoPathEntry.Text = _configService.RepoPath;
            BranchNameEntry.Text = _configService.BranchName;

            // Restore the selected environment
            if (!string.IsNullOrEmpty(_configService.SelectedEnvironment))
            {
                EnvironmentPicker.SelectedItem = _configService.SelectedEnvironment;
            }

            if (!string.IsNullOrWhiteSpace(_configService.RepoPath))
            {
                _gitService.Configure(_configService.RepoPath, _configService.BranchName);
                try
                {
                    StatusEditor.Text = "validating...";
                    await _gitService.Prepare();
                    StatusEditor.Text = "Repository is valid.";
                }
                catch (Exception ex)
                {
                    StatusEditor.Text = ex.Message;
                }
            }
        }

        private void SaveConfiguration()
        {
            _configService.RepoPath = RepoPathEntry.Text;
            _configService.BranchName = BranchNameEntry.Text;

            // Save the selected environment
            _configService.SelectedEnvironment = EnvironmentPicker.SelectedItem?.ToString() ?? "";

            _configService.SaveConfig();
        }

        private async Task ValidateInputsAsync()
        {
            StatusEditor.Text = "validating...";

            var repoPathValid = !string.IsNullOrWhiteSpace(RepoPathEntry.Text);
            var branchNameValid = !string.IsNullOrWhiteSpace(BranchNameEntry.Text);
            var environmentSelected = EnvironmentPicker.SelectedItem != null;

            // Validate the repository path by calling GitService.Prepare
            if (repoPathValid && branchNameValid)
            {
                try
                {
                    _gitService.Configure(RepoPathEntry.Text, BranchNameEntry.Text);
                    await _gitService.Prepare();
                    StatusEditor.Text = "Repository is valid.";
                }
                catch (Exception ex)
                {
                    StatusEditor.Text = ex.Message;
                    repoPathValid = false;
                }
            }

            GetStatusButton.IsEnabled = repoPathValid && environmentSelected;
            DeployButton.IsEnabled = repoPathValid && environmentSelected;
        }

        private async void OnGetStatusClicked(object sender, EventArgs e)
        {
            try
            {
                StatusEditor.Text = "Getting status...";
                await _gitService.Prepare();
                var environment = EnvironmentPicker.SelectedItem.ToString()!;
                var status = await _gitService.GetStatus(environment);

                var statusBuilder = new StringBuilder();

                if (status.CurrentCommit != null)
                {
                    statusBuilder.AppendLine("<strong>Newest commit deployed:</strong>");
                    statusBuilder.AppendLine($"    {status.CurrentCommit.Hash} | {status.CurrentCommit.Date:yyyy-MM-dd HH:mm:ss} | {status.CurrentCommit.Author} | {status.CurrentCommit.Message}");
                }
                else
                {
                    statusBuilder.AppendLine("No deployed commit found.");
                }

                if (status.PendingCommits.Any())
                {
                    statusBuilder.AppendLine($"\n<strong>Missing commits from {_configService.BranchName}:</strong>");
                    foreach (var commit in status.PendingCommits)
                    {
                        statusBuilder.AppendLine($"    {commit.Hash} | {commit.Date:yyyy-MM-dd HH:mm:ss} | {commit.Author} | {commit.Message}");
                    }
                }
                else
                {
                    statusBuilder.AppendLine("\n<strong>All commits are deployed.</strong>");
                }

                StatusEditor.Text = statusBuilder.ToString();
            }
            catch (Exception ex)
            {
                StatusEditor.Text = ex.Message;
            }
        }

        private async void OnDeployClicked(object sender, EventArgs e)
        {
            try
            {
                StatusEditor.Text = "Deploying...";
                await _gitService.Prepare();
                var environment = EnvironmentPicker.SelectedItem.ToString()!;
                await _gitService.Deploy(environment);
                StatusEditor.Text = $"Deploy to {environment} completed.";
            }
            catch (Exception ex)
            {
                StatusEditor.Text = ex.Message;
            }
        }

        private async void RepoPathEntry_Unfocused(object sender, FocusEventArgs e)
        {
            StatusEditor.Text = "validating...";
            _gitService.Configure(RepoPathEntry.Text, BranchNameEntry.Text);

            try
            {
                await _gitService.Prepare();
                StatusEditor.Text = "Repository is valid.";
            }
            catch (Exception ex)
            {
                StatusEditor.Text = ex.Message;
            }

            SaveConfiguration();
            await ValidateInputsAsync();
        }

        private async void BranchNameEntry_Unfocused(object sender, FocusEventArgs e)
        {
            SaveConfiguration();
            await ValidateInputsAsync();
        }

        private async void EnvironmentPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveConfiguration();
            await ValidateInputsAsync();
        }

        protected override void OnDisappearing()
        {
            SaveConfiguration();
            base.OnDisappearing();
        }
    }
}
