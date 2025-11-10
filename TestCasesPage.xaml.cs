using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFApp
{
    public partial class TestCasesPage : UserControl
    {
        private readonly ApiClient _apiClient;
        private readonly string _token;
        private readonly string _projectId;
        private readonly string _projectTitle;
        private readonly Action<UserControl> _navigateTo;

        public TestCasesPage(string token, string projectId, string projectTitle, Action<UserControl> navigateTo = null)
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _token = token;
            _projectId = projectId;
            _projectTitle = projectTitle;
            _navigateTo = navigateTo;

            _apiClient.SetBearer(_token);

            if (btnBack != null)
            {
                btnBack.Click += btnBack_Click;
            }

            LoadTestCases();
        }

        private async void LoadTestCases()
        {
            try
            {
                var response = await _apiClient.GetAsync("GetTestCases", new Dictionary<string, string> { { "projectId", _projectId ?? string.Empty } });

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonResponse);

                    txtProjectTitle.Text = _projectTitle ?? "";
                    dgTestCases.ItemsSource = testCases ?? new List<TestCase>();
                }
                else
                {
                    MessageBox.Show("Failed to load test cases.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading test cases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewTestSteps_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            var testcaseId = btn.Tag as string;
            if (string.IsNullOrEmpty(testcaseId)) return;

            if (_navigateTo != null)
                _navigateTo(new TestStepsPage(_token, testcaseId, _projectId, _projectTitle, _navigateTo));
            else
            {
                var window = Window.GetWindow(this);
                if (window is DashboardWindow dashboard)
                    dashboard.contentArea.Content = new TestStepsPage(_token, testcaseId, _projectId, _projectTitle, null);
            }
        }

        // New: Generate & Execute handler - navigates to GenerateScriptPage and starts the flow
        private async void GenerateAndExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            var testcaseId = btn.Tag as string;
            if (string.IsNullOrEmpty(testcaseId)) return;

            var generatePage = new GenerateScriptPage(_token);

            if (_navigateTo != null)
            {
                _navigateTo(generatePage);
                await generatePage.StartForTestCase(testcaseId);
            }
            else
            {
                var window = Window.GetWindow(this);
                if (window is DashboardWindow dashboard)
                {
                    dashboard.contentArea.Content = generatePage;
                    await generatePage.StartForTestCase(testcaseId);
                }
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_navigateTo != null)
                _navigateTo(new ProjectsPage(_token, _navigateTo));
            else
            {
                var window = Window.GetWindow(this);
                if (window is DashboardWindow dashboard)
                    dashboard.contentArea.Content = new ProjectsPage(_token, null);
            }
        }
    }
}