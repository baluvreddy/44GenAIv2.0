using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFApp
{
    public partial class TestStepsPage : UserControl
    {
        private readonly ApiClient _apiClient;
        private readonly string _token;
        private readonly string _testCaseId;
        private readonly string _projectTitle;
        private readonly string _projectId;
        private readonly Action<UserControl> _navigateTo;

        // Added projectId parameter and store it in _projectId
        public TestStepsPage(string token, string testCaseId, string projectId, string projectTitle, Action<UserControl> navigateTo)
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _token = token;
            _apiClient.SetBearer(_token);

            _testCaseId = testCaseId;
            _projectId = projectId;
            _projectTitle = projectTitle;
            _navigateTo = navigateTo;
            LoadTestSteps();
        }

        private async void LoadTestSteps()
        {
            try
            {
                // Use a concrete REST template instead of a config key so the request resolves correctly.
                var response = await _apiClient.GetAsync($"testcases/{Uri.EscapeDataString(_testCaseId)}/steps");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var testStepsResponse = JsonSerializer.Deserialize<TestStepsResponse>(jsonResponse);

                    if (testStepsResponse == null)
                    {
                        MessageBox.Show("Failed to deserialize test steps response.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    txtTestCaseTitle.Text = $"Test Steps for Test Case {_testCaseId} (Project: {_projectTitle})";
                    var stepsList = new List<TestStep>();
                    if (testStepsResponse.steps != null)
                    {
                        for (int i = 0; i < testStepsResponse.steps.Length; i++)
                        {
                            stepsList.Add(new TestStep
                            {
                                StepNumber = i + 1,
                                StepDescription = testStepsResponse.steps[i] ?? string.Empty,
                                Argument = (testStepsResponse.args != null && i < testStepsResponse.args.Length) ? testStepsResponse.args[i] ?? string.Empty : string.Empty
                            });
                        }
                    }
                    lvTestSteps.ItemsSource = stepsList;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = "Failed to load test steps.";
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<ErrorResponse>(errorContent);
                        errorMessage = errorJson?.detail ?? errorMessage;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing error response: {ex.Message}");
                    }
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading test steps: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_navigateTo != null)
            {
                _navigateTo(new TestCasesPage(_token, _projectId, _projectTitle, _navigateTo));
                return;
            }

            var window = Window.GetWindow(this);
            if (window is DashboardWindow dashboard)
            {
                dashboard.contentArea.Content = new TestCasesPage(_token, _projectId, _projectTitle, null);
            }
        }
    }

    // Class to deserialize the API response
    public class TestStepsResponse
    {
        public string? testcaseid { get; set; }
        public string?[]? steps { get; set; }
        public string?[]? args { get; set; }
        public int stepnum { get; set; }
    }

    // Class for ListView binding
    public class TestStep
    {
        public int StepNumber { get; set; }
        public string? StepDescription { get; set; }
        public string? Argument { get; set; }
    }
}
