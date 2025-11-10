using System;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;

namespace MyWPFApp
{
    public partial class CreateProjectPage : UserControl
    {
        private readonly ApiClient _apiClient;
        private readonly string _token;
        private readonly Action _onSuccess;

        public CreateProjectPage(string token, Action onSuccess)
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _token = token;
            _apiClient.SetBearer(_token);
            _onSuccess = onSuccess; // Callback to refresh projects page or navigate back
        }

        private async void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(txtTitle.Text) || txtTitle.Text == "Enter project title")
                {
                    MessageBox.Show("Please enter a valid project title.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtStartDate.Text) || txtStartDate.Text == "YYYY-MM-DD" || !DateTime.TryParse(txtStartDate.Text, out _))
                {
                    MessageBox.Show("Please enter a valid start date (YYYY-MM-DD).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (cmbProjectType.SelectedItem == null)
                {
                    MessageBox.Show("Please select a project type.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtDescription.Text) || txtDescription.Text == "Enter description")
                {
                    MessageBox.Show("Please enter a description.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Prepare the API request payload
                var selectedItem = cmbProjectType.SelectedItem as ComboBoxItem;
                string? projectType = selectedItem?.Content?.ToString();
                if (string.IsNullOrWhiteSpace(projectType))
                {
                    MessageBox.Show("Please select a valid project type.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var projectRequest = new
                {
                    title = txtTitle.Text,
                    startdate = txtStartDate.Text,
                    projecttype = projectType,
                    description = txtDescription.Text
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(projectRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send POST request
                var response = await _apiClient.PostAsync("CreateProject", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var projectResponse = JsonSerializer.Deserialize<Project>(jsonResponse);

                    MessageBox.Show("Project created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    _onSuccess?.Invoke();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = "Failed to create project.";
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<ErrorResponse>(errorContent);
                        errorMessage = errorJson?.detail ?? errorMessage;
                    }
                    catch
                    {
                    }
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // On cancel navigate back via the provided callback
            _onSuccess?.Invoke();
        }
    }
}
