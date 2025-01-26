using DeployManager.GitHelper;
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

            _gitService.GitCommandStarted += (_, args) =>
            {
                AppendToStatusEditor($"<br/>&gt; git {args.Arguments}...");
                ActivityIndicator.IsRunning = true;
                ActivityIndicator.IsVisible = true;
            };
            _gitService.GitCommandFinished += (_, args) =>
            {
                AppendToStatusEditor($"&nbsp;{(args.ExitCode == 0 ? "ok." : "error.")}");
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
                _gitService.Configure(_configService.RepoPath);
                try
                {
                    AppendToStatusEditor("<p>validating...</p>", true);
                    await _gitService.Prepare();
                    AppendToStatusEditor("<p>Repository is valid.</p>");
                }
                catch (Exception ex)
                {
                    AppendToStatusEditor($"<p>{ex.Message}</p>, true");
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
            AppendToStatusEditor("<p>validating...</p>", true);

            var repoPathValid = !string.IsNullOrWhiteSpace(RepoPathEntry.Text);
            var branchNameValid = !string.IsNullOrWhiteSpace(BranchNameEntry.Text);
            var environmentSelected = EnvironmentPicker.SelectedItem != null;

            // Validate the repository path by calling GitService.Prepare
            if (repoPathValid && branchNameValid)
            {
                try
                {
                    _gitService.Configure(RepoPathEntry.Text);
                    await _gitService.Prepare();
                    AppendToStatusEditor("<p>Repository is valid.</p>", true);
                }
                catch (Exception ex)
                {
                    AppendToStatusEditor($"<p>{ex.Message}</p>", true);
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
                AppendToStatusEditor("<p>Getting status...</p>", true);
                await _gitService.Prepare();
                var environment = EnvironmentPicker.SelectedItem.ToString()!;
                var branch = BranchNameEntry.Text;
                var status = await _gitService.GetStatus(environment, branch);

                var statusBuilder = new StringBuilder();

                if (status.CurrentCommit != null)
                {
                    statusBuilder.AppendLine("<p><strong>Newest commit deployed:</strong></p>");
                    statusBuilder.AppendLine($"<p>{status.CurrentCommit.Hash} | {status.CurrentCommit.Date:yyyy-MM-dd HH:mm:ss} | {status.CurrentCommit.Author} | {status.CurrentCommit.Message}</p>");
                }
                else
                {
                    statusBuilder.AppendLine("<p>No deployed commit found.</p>");
                }

                if (status.PendingCommits.Any())
                {
                    statusBuilder.AppendLine($"<p><strong>Missing commits from \"{branch}\":</strong></p>");
                    statusBuilder.AppendLine("<ul>");
                    foreach (var commit in status.PendingCommits)
                    {
                        statusBuilder.AppendLine($"<li>{commit.Hash} | {commit.Date:yyyy-MM-dd HH:mm:ss} | {commit.Author} | {commit.Message}</li>");
                    }
                    statusBuilder.AppendLine("</ul>");
                }
                else
                {
                    statusBuilder.AppendLine($"<p><strong>All commits are deployed. The environment \"{environment}\" is up to date based on the branch \"{branch}\"</strong></p>");
                }

                AppendToStatusEditor(statusBuilder.ToString(), true);
            }
            catch (Exception ex)
            {
                AppendToStatusEditor($"<p>{ex.Message}</p>");
            }
        }

        private void StatusEditor_Navigating(object sender, WebNavigatingEventArgs e)
        {
            // Check if the navigation is triggered by an external link
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri))
            {
                if(uri.Scheme != "http" && uri.Scheme != "https")
                {
                    return;
                }

                e.Cancel = true; // Prevent the WebView from navigating
                try
                {
                    // Open the link in the default browser
#if ANDROID
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView, Android.Net.Uri.Parse(uri.ToString()));
            Android.App.Application.Context.StartActivity(intent);
#elif IOS
            UIKit.UIApplication.SharedApplication.OpenUrl(new Foundation.NSUrl(uri.ToString()));
#elif WINDOWS
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = uri.ToString(),
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
#endif
                }
                catch (Exception ex)
                {
                    // Handle errors if needed
                    Console.WriteLine($"Failed to open URL: {ex.Message}");
                }
            }
        }

        private async void OnDeployClicked(object sender, EventArgs e)
        {
            try
            {
                AppendToStatusEditor("<p>Deploying...</p>", true);
                await _gitService.Prepare();
                var environment = EnvironmentPicker.SelectedItem.ToString()!;
                var branch = BranchNameEntry.Text;

                await _gitService.Deploy(environment, branch);
                AppendToStatusEditor($"<p>Deploy to {environment} completed.</p>");
            }
            catch (Exception ex)
            {
                AppendToStatusEditor($"<p>{ex.Message}</p>");
            }
        }

        private async void RepoPathEntry_Unfocused(object sender, FocusEventArgs e)
        {
            AppendToStatusEditor("<p>validating...</p>", true);
            _gitService.Configure(RepoPathEntry.Text);

            try
            {
                await _gitService.Prepare();
                AppendToStatusEditor("<p>Repository is valid.</p>");
            }
            catch (Exception ex)
            {
                AppendToStatusEditor($"<p>{ex.Message}</p>");
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

        private void AppendToStatusEditor(string htmlContent, bool clear = false)
        {
            const string baseHtml = "<html><head><style>body { font-family: monospace; }</style></head><body></body></html>";

            if (clear || StatusEditor.Source is not HtmlWebViewSource htmlSource)
            {
                StatusEditor.Source = new HtmlWebViewSource { Html = baseHtml.Replace("</body>", htmlContent + "</body>") };
                return;
            }

            var currentHtml = htmlSource.Html ?? baseHtml;
            var updatedHtml = currentHtml.Replace("</body>", htmlContent + "</body>");
            StatusEditor.Source = new HtmlWebViewSource { Html = updatedHtml };
        }

        protected override void OnDisappearing()
        {
            SaveConfiguration();
            base.OnDisappearing();
        }
    }
}
