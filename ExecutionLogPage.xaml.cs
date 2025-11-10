using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFApp
{
    public partial class ExecutionLogPage : UserControl
    {
        private readonly ApiClient _apiClient;
        private readonly string _token;

        public ExecutionLogPage(string token)
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _token = token;
            _apiClient.SetBearer(_token);
            LoadExecutionLogs();
        }

        private async void LoadExecutionLogs()
        {
            try
            {
                var response = await _apiClient.GetAsync("ExecutionLogs");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var executionLogs = JsonSerializer.Deserialize<List<ExecutionLog>>(jsonResponse);

                    dgExecutionLogs.ItemsSource = executionLogs;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = "Failed to load execution logs.";
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
                MessageBox.Show($"Error loading execution logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}