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

            AppendToWebView(GitCommands, "");
            AppendToWebView(StatusEditor, "");

            _gitService.GitCommandStarted += (_, args) =>
            {
                AppendToWebView(GitCommands, $"<br/>&gt; git {args.Arguments}...");
                ActivityIndicator.IsRunning = true;
                ActivityIndicator.IsVisible = true;
            };
            _gitService.GitCommandFinished += (_, args) =>
            {
                AppendToWebView(GitCommands, $"&nbsp;{(args.ExitCode == 0 ? "ok." : "error.")}");
                ActivityIndicator.IsRunning = false;
                ActivityIndicator.IsVisible = false;
            };
            // Load configuration on startup
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            _configService = ConfigService.LoadConfig();

            RepoPathEntry.Text = _configService.RepoPath;
            BranchNameEntry.Text = _configService.BranchName;

            // Restore the selected environment
            if (!string.IsNullOrEmpty(_configService.SelectedEnvironment))
            {
                EnvironmentPicker.SelectedItem = _configService.SelectedEnvironment;
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

        private async void OnGetStatusClicked(object sender, EventArgs e)
        {
            try
            {
                AppendToWebView(GitCommands, "", true);
                AppendToWebView(StatusEditor, "<p>Getting status...</p>", true);
                var environment = EnvironmentPicker.SelectedItem.ToString()!;
                var branch = BranchNameEntry.Text;
                await _gitService.Prepare();

                var status = await _gitService.GetStatus();

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

                AppendToWebView(StatusEditor, statusBuilder.ToString(), true);
            }
            catch (Exception ex)
            {
                AppendToWebView(StatusEditor, $"<p>{ex.Message}</p>");
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
                AppendToWebView(GitCommands, "", true);
                AppendToWebView(StatusEditor, "<p>Deploying...</p>", true);
                var environment = EnvironmentPicker.SelectedItem.ToString()!;
                var branch = BranchNameEntry.Text;

                await _gitService.Prepare();

                await _gitService.Deploy();
                AppendToWebView(StatusEditor, $"<p>Deploy to {environment} has been started. Check circleci and argocd for progress</p>");
            }
            catch (Exception ex)
            {
                AppendToWebView(StatusEditor, $"<p>{ex.Message}</p>");
            }
        }

        private void UpdateGitService(object sender, EventArgs e)
        {
            _gitService.RepoPath = RepoPathEntry.Text;
            _gitService.Branch = BranchNameEntry.Text;
            _gitService.Environment = EnvironmentPicker.SelectedItem.ToString();
        }


        private void AppendToWebView(WebView webView, string htmlContent, bool clear = false)
        {
            const string baseHtml = "<html><head><style>body { font-family: monospace; }</style></head><body></body></html>";

            if (clear || webView.Source is not HtmlWebViewSource htmlSource)
            {
                webView.Source = new HtmlWebViewSource { Html = baseHtml.Replace("</body>", htmlContent + "</body>") };
                return;
            }

            var currentHtml = htmlSource.Html ?? baseHtml;
            var updatedHtml = currentHtml.Replace("</body>", htmlContent + "</body>");
            webView.Source = new HtmlWebViewSource { Html = updatedHtml };
        }


        protected override void OnDisappearing()
        {
            SaveConfiguration();
            base.OnDisappearing();
        }
    }
}
