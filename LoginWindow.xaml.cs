using System;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Net.Http;

namespace MyWPFApp
{
    public partial class LoginWindow : Window
    {
        private readonly ApiClient _apiClient;

        public LoginWindow()
        {
            InitializeComponent();
            _apiClient = new ApiClient();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prepare the API request payload
                var loginRequest = new
                {
                    username = txtUsername.Text,
                    password = txtPassword.Password
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send POST request to the API (use lowercase 'login/' to match backend route)
                var response = await _apiClient.PostAsync("login/", content);

                if (response.IsSuccessStatusCode)
                {
                    // Deserialize the response
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(jsonResponse);

                    // Show success message and navigate to Dashboard
                    MessageBox.Show("Login successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Open Dashboard window and pass the response data if needed
                    var dashboard = new DashboardWindow(loginResponse);
                    dashboard.Show();
                    this.Close(); // Close the login window
                }
                else
                {
                    MessageBox.Show("Invalid username or password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Register window
            var registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
        }
    }

    // Classes to deserialize the API response
    public class LoginResponse
    {
        public string userid { get; set; }
        public string role { get; set; }
        public string token { get; set; }
        public Project[] projects { get; set; }
    }

    public class Project
    {
        public string projectid { get; set; }
        public string title { get; set; }
        public string startdate { get; set; }
        public string projecttype { get; set; }
        public string description { get; set; }
    }
}