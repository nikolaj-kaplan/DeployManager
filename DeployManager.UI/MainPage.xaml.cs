namespace DeployManager.UI
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnGetStatusClicked(object sender, EventArgs e)
        {
            var environment = EnvironmentPicker.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(environment))
            {
                await DisplayAlert("Error", "Please select an environment.", "OK");
                return;
            }

            try
            {
                var commits = await GitHelper.GetStatus(environment);
                StatusEditor.Text = string.Join(Environment.NewLine, commits);
            }
            catch (Exception ex)
            {
                StatusEditor.Text = $"Error: {ex.Message}";
            }
        }

        private async void OnDeployClicked(object sender, EventArgs e)
        {
            var environment = EnvironmentPicker.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(environment))
            {
                await DisplayAlert("Error", "Please select an environment.", "OK");
                return;
            }

            try
            {
                await GitHelper.Deploy(environment);
                StatusEditor.Text = $"Deploy to {environment} completed.";
            }
            catch (Exception ex)
            {
                StatusEditor.Text = $"Error: {ex.Message}";
            }
        }
    }
}
