using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFApp
{
    public partial class RegisterWindow : Window
    {
        private readonly ApiClient _apiClient;

        public RegisterWindow()
        {
            InitializeComponent();
            _apiClient = new ApiClient();
        }

        private async void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(txtName.Text) || txtName.Text == "Enter name")
                {
                    MessageBox.Show("Please enter a valid name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtMail.Text) || txtMail.Text == "Enter email" || !txtMail.Text.Contains("@"))
                {
                    MessageBox.Show("Please enter a valid email.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("Please enter a password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (cmbRole.SelectedItem == null)
                {
                    MessageBox.Show("Please select a role.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Prepare the API request payload
                var registerRequest = new
                {
                    name = txtName.Text,
                    mail = txtMail.Text,
                    password = txtPassword.Password,
                    role = (cmbRole.SelectedItem as ComboBoxItem)?.Content.ToString()
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(registerRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send POST request to the API using centralized ApiClient
                var response = await _apiClient.PostAsync("user/", content);

                if (response.IsSuccessStatusCode)
                {
                    // Deserialize the response
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var registerResponse = JsonSerializer.Deserialize<RegisterResponse>(jsonResponse);

                    // Show success message and navigate to Login
                    MessageBox.Show($"Registration successful for {registerResponse.name}!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
                else
                {
                    // Handle error response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = "Registration failed. Please try again.";
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<ErrorResponse>(errorContent);
                        errorMessage = errorJson?.detail ?? errorMessage;
                    }
                    catch
                    {
                        // Fallback if error response is not JSON or doesn't match schema
                    }
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Login window
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }

    // Classes to deserialize the API response
    public class RegisterResponse
    {
        public string name { get; set; }
        public string mail { get; set; }
        public string userid { get; set; }
        public string role { get; set; }
    }

    // Class for error response (generic, adjust based on your API's error format)
    public class ErrorResponse
    {
        public string detail { get; set; }
    }
}