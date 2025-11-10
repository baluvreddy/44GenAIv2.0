using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace MyWPFApp
{
    public partial class GenerateScriptPage : UserControl
    {
        private readonly string _token;
        private readonly ApiClient _apiClient;
        private string _downloadedTestPlanPath;
        private readonly List<ExecutionLogEntry> _logs = new();
        // Changed to ObservableCollection so the DataGrid receives change notifications
        private readonly ObservableCollection<TestPlanItem> _testPlanItems = new();

        public class TestPlanItem
        {
            public string testCaseId { get; set; }
            public string step { get; set; }
            public string testData { get; set; }
        }

        public GenerateScriptPage(string token)
        {
            InitializeComponent();
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token), "Token cannot be null or empty.");
            _token = token;
            _apiClient = new ApiClient();
            _apiClient.SetBearer(_token);

            // Bind the DataGrid once to the ObservableCollection so UI updates automatically
            dgTestPlan.ItemsSource = _testPlanItems;

            InitializeControls();
            VerifyEndpoints();
        }

        private void InitializeControls()
        {
            btnFetchTestPlan.Click += BtnFetchTestPlan_Click;
            btnExit.Click += BtnExit_Click;

            txtTestCaseId.Text = "Enter Test Case ID";
            txtTestCaseId.Foreground = System.Windows.Media.Brushes.Gray;
            txtTestCaseId.GotFocus += (s, e) =>
            {
                if (txtTestCaseId.Text == "Enter Test Case ID")
                {
                    txtTestCaseId.Text = "";
                    txtTestCaseId.Foreground = System.Windows.Media.Brushes.Black;
                }
                Console.WriteLine($"GotFocus: Text = '{txtTestCaseId.Text}' at {DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}");
            };
            txtTestCaseId.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtTestCaseId.Text))
                {
                    txtTestCaseId.Text = "Enter Test Case ID";
                    txtTestCaseId.Foreground = System.Windows.Media.Brushes.Gray;
                }
                Console.WriteLine($"LostFocus: Text = '{txtTestCaseId.Text}' at {DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")}");
            };
        }

        private async void VerifyEndpoints()
        {
            _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "Verifying endpoints...", status = "INFO" });
            UpdateLogView();
            try
            {
                // Use ApiClient and its endpoint templates
                var testPlanResponse = await _apiClient.GetAsync("TestPlan", new Dictionary<string, string> { ["testCaseId"] = "TC0013" });
                var executeCodeResponse = await _apiClient.GetAsync("ExecuteCode", new Dictionary<string, string> { ["script_type"] = "playwright" });

                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Test Plan endpoint: {(testPlanResponse.IsSuccessStatusCode ? "Connected" : "Failed")}", status = testPlanResponse.IsSuccessStatusCode ? "SUCCESS" : "ERROR" });
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Execute Code endpoint: {(executeCodeResponse.IsSuccessStatusCode ? "Connected" : "Failed")}", status = executeCodeResponse.IsSuccessStatusCode ? "SUCCESS" : "ERROR" });
                UpdateLogView();
            }
            catch (Exception ex)
            {
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Endpoint verification failed: {ex.Message}", status = "ERROR" });
                UpdateLogView();
                Console.WriteLine($"Endpoint verification exception: {ex.ToString()}");
            }
        }

        // New: public entry to start generation+execution for a given test case id from other pages
        public async Task StartForTestCase(string testCaseId)
        {
            if (string.IsNullOrWhiteSpace(testCaseId)) return;

            // Set UI state first
            txtTestCaseId.Text = testCaseId;
            spTestPlan.Visibility = Visibility.Collapsed;
            tabExecutionLogs.Visibility = Visibility.Visible;
            tabControl.SelectedItem = tabExecutionLogs;

            await FetchAndExecuteTestPlanAsync(testCaseId);
        }

        private async void BtnFetchTestPlan_Click(object sender, RoutedEventArgs e)
        {
            string testCaseId = txtTestCaseId.Text.Trim();
            await FetchAndExecuteTestPlanAsync(testCaseId);
        }

        // Extracted the fetch+generate+execute flow so it can be reused by StartForTestCase
        private async Task FetchAndExecuteTestPlanAsync(string testCaseId)
        {
            Console.WriteLine("FetchAndExecuteTestPlanAsync triggered at " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            try
            {
                if (string.IsNullOrWhiteSpace(testCaseId) || testCaseId == "Enter Test Case ID")
                {
                    MessageBox.Show("Please enter a valid Test Case ID (e.g., TC0013).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var statusLabel = ((Grid)this.Content).FindName("txtStatus") as TextBlock;
                if (statusLabel != null) statusLabel.Text = "Fetching test plan...";
                _logs.Clear(); // Clear previous logs
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Fetching test plan for {testCaseId}", status = "INFO" });
                UpdateLogView();

                var response = await _apiClient.GetAsync("TestPlan", new Dictionary<string, string> { ["testCaseId"] = testCaseId });
                var rawResponse = await response.Content.ReadAsByteArrayAsync();
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Received response with {rawResponse.Length} bytes", status = "DEBUG" });
                UpdateLogView();

                if (response.IsSuccessStatusCode)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), $"testplan_{testCaseId}.json");
                    await File.WriteAllBytesAsync(tempPath, rawResponse);
                    _downloadedTestPlanPath = tempPath;

                    string jsonContent = await File.ReadAllTextAsync(tempPath);
                    var jsonDoc = JsonDocument.Parse(jsonContent);

                    // Clear and repopulate the ObservableCollection so UI updates automatically
                    _testPlanItems.Clear();

                    // Parse "current - bdd steps" for the current test case
                    if (jsonDoc.RootElement.TryGetProperty("current testid", out JsonElement currentTestId) &&
                        jsonDoc.RootElement.TryGetProperty("current - bdd steps", out JsonElement currentBddSteps))
                    {
                        string currentTestIdValue = currentTestId.GetString();
                        foreach (var step in currentBddSteps.EnumerateObject())
                        {
                            _testPlanItems.Add(new TestPlanItem
                            {
                                testCaseId = currentTestIdValue,
                                step = step.Name,
                                testData = step.Value.GetString() ?? ""
                            });
                        }
                    }

                    spTestPlan.Visibility = Visibility.Visible;
                    tabExecutionLogs.Visibility = Visibility.Visible;
                    tabControl.SelectedItem = tabExecutionLogs;

                    // Generate script on frontend
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "Generating script...", status = "INFO" });
                    UpdateLogView();
                    string generatedScript = GenerateScriptFromBddSteps(jsonContent);
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "Script generated successfully", status = "SUCCESS" });
                    UpdateLogView();

                    // Execute script
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "Running script...", status = "INFO" });
                    UpdateLogView();
                    if (statusLabel != null) statusLabel.Text = "Running script...";
                    await ExecuteScript(generatedScript, "playwright"); // Hardcoded to Playwright for now
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "Script execution completed", status = "SUCCESS" });
                    UpdateLogView();
                }
                else
                {
                    string errorMessage = "Failed to fetch test plan.";
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<ErrorResponse>(await response.Content.ReadAsStringAsync());
                        errorMessage = errorJson?.detail ?? errorMessage;
                    }
                    catch (Exception ex)
                    {
                        _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Error parsing error response: {ex.Message}", status = "DEBUG" });
                    }
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = errorMessage, status = "ERROR" });
                    UpdateLogView();
                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Error: {ex.Message}", status = "ERROR" });
                UpdateLogView();
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Exception: {ex.ToString()}");
            }
            finally
            {
                var statusLabel = ((Grid)this.Content).FindName("txtStatus") as TextBlock;
                if (statusLabel != null) statusLabel.Text = "Ready";
            }
        }

        private string GenerateScriptFromBddSteps(string jsonContent)
        {
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var scriptBuilder = new StringBuilder();
            scriptBuilder.AppendLine("import asyncio");
            scriptBuilder.AppendLine("import re"); // <-- ensure 're' is imported (fixes runtime NameError)
            scriptBuilder.AppendLine("from playwright.async_api import async_playwright, expect");
            scriptBuilder.AppendLine();
            scriptBuilder.AppendLine("async def run_test():");
            scriptBuilder.AppendLine("    \"\"\"Test Case for current test ID.\"\"\"");
            scriptBuilder.AppendLine("    async with async_playwright() as p:");
            scriptBuilder.AppendLine("        browser = await p.chromium.launch(headless=False)");
            scriptBuilder.AppendLine("        page = await browser.new_page()");
            scriptBuilder.AppendLine("        try:");

            if (jsonDoc.RootElement.TryGetProperty("current - bdd steps", out JsonElement currentBddSteps))
            {
                foreach (var step in currentBddSteps.EnumerateObject())
                {
                    string stepText = step.Name;
                    string testData = (step.Value.GetString() ?? "").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                    switch (stepText)
                    {
                        case "Given the user is on the OrangeHRM login page":
                            scriptBuilder.AppendLine("            print(\"Step: Navigating to the OrangeHRM login page...\")");
                            scriptBuilder.AppendLine($"            await page.goto(\"{testData}\")");
                            break;
                        case "When the user enters the username":
                        case "And enters their username":
                            scriptBuilder.AppendLine("            print(\"Step: Entering username...\")");
                            scriptBuilder.AppendLine($"            await page.get_by_placeholder(\"Username\").fill(\"{testData}\")");
                            break;
                        case "And the user enters the password":
                            scriptBuilder.AppendLine("            print(\"Step: Entering password...\")");
                            scriptBuilder.AppendLine($"            await page.get_by_placeholder(\"Password\").fill(\"{testData}\")");
                            break;
                        case "And clicks the login button":
                        case "And clicks the 'Reset Password' button":
                            scriptBuilder.AppendLine("            print(\"Step: Clicking the button...\")");
                            scriptBuilder.AppendLine("            await page.get_by_role(\"button\", name=\"Login\").click()");
                            break;
                        case "When the user clicks on 'Forgot your Password?'":
                            scriptBuilder.AppendLine("            print(\"Step: Clicking 'Forgot your Password?'...\")");
                            scriptBuilder.AppendLine("            await page.get_by_text(\"Forgot your Password?\").click()");
                            break;
                        case "Then the user should be redirected to the dashboard":
                            scriptBuilder.AppendLine("            print(\"Step: Verifying redirection to the dashboard...\")");
                            scriptBuilder.AppendLine("            await expect(page).to_have_url(re.compile(r\".*/dashboard/index\"))");
                            scriptBuilder.AppendLine("            dashboard_header = page.get_by_role(\"heading\", name=\"Dashboard\")");
                            scriptBuilder.AppendLine("            await expect(dashboard_header).to_be_visible()");
                            break;
                        case "Then a password reset link should be sent successfully":
                            scriptBuilder.AppendLine("            print(\"Step: Verifying password reset link...\")");
                            scriptBuilder.AppendLine("            await expect(page.get_by_text(\"Reset link sent successfully\")).to_be_visible()");
                            break;
                    }
                }
            }

            scriptBuilder.AppendLine("            print(\"Test Passed - Execution completed successfully.\")");
            scriptBuilder.AppendLine("        except Exception as e:");
            scriptBuilder.AppendLine("            print(f\"Test Failed - An error occurred: {str(e)}\")");
            scriptBuilder.AppendLine("        finally:");
            scriptBuilder.AppendLine("            await browser.close()");
            scriptBuilder.AppendLine();
            scriptBuilder.AppendLine("if __name__ == \"__main__\":");
            scriptBuilder.AppendLine("    asyncio.run(run_test())");

            return scriptBuilder.ToString();
        }

        private async Task ExecuteScript(string script, string scriptType)
        {
            using var formData = new MultipartFormDataContent();
            var fileContent = new StringContent(script, Encoding.UTF8, "text/x-python");
            formData.Add(fileContent, "file", $"{txtTestCaseId.Text}_script.py");

            // Use ApiClient's templated endpoint resolution
            var response = await _apiClient.PostAsync("ExecuteCode", formData, new Dictionary<string, string> { ["script_type"] = scriptType } );
            var rawResponse = await response.Content.ReadAsStringAsync();
            _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Raw response: {rawResponse.Substring(0, Math.Min(rawResponse.Length, 200))}...", status = "DEBUG" });
            UpdateLogView();

            if (response.IsSuccessStatusCode)
            {
                var logsText = rawResponse;
                var logLines = logsText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in logLines)
                {
                    string status = line.Contains("Test Passed") ? "SUCCESS" : (line.Contains("Test Failed") ? "ERROR" : "INFO");
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = line.Trim(), status = status });
                }
                UpdateLogView();
            }
            else
            {
                string errorMessage = "Failed to execute script.";
                try
                {
                    var errorJson = JsonSerializer.Deserialize<ErrorResponse>(rawResponse);
                    errorMessage = errorJson?.detail ?? errorMessage;
                }
                catch (Exception ex)
                {
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Error parsing error response: {ex.Message}", status = "DEBUG" });
                }
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = errorMessage, status = "ERROR" });
                UpdateLogView();
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateLogView()
        {
            var logListView = ((Grid)this.Content).FindName("lvExecutionLogs") as ListView;
            if (logListView != null)
            {
                logListView.ItemsSource = null;
                logListView.ItemsSource = _logs;
                if (logListView.Items.Count > 0)
                {
                    logListView.ScrollIntoView(logListView.Items[logListView.Items.Count - 1]);
                }
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window is DashboardWindow dashboard)
            {
                dashboard.contentArea.Content = new ExecutionLogPage(_token);
            }
        }
    }
}
