using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyWPFApp
{
    public partial class TestExecutionPage : UserControl, IDisposable
    {
        private readonly string _token;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly List<ExecutionLogEntry> _logs = new();
        private readonly ApiClient _apiClient = new();
        private const int WEBSOCKET_TIMEOUT_MS = 30000; // 30 second timeout

        public TestExecutionPage(string token)
        {
            InitializeComponent();
            _token = token;
            txtTestCaseId.GotFocus += (s, e) => { if (txtTestCaseId.Text == "Enter Test Case ID") txtTestCaseId.Text = ""; };
            txtTestCaseId.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtTestCaseId.Text)) txtTestCaseId.Text = "Enter Test Case ID"; };
        }

        private async void btnExecute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? testCaseId = txtTestCaseId.Text?.Trim();
                if (string.IsNullOrWhiteSpace(testCaseId) || testCaseId == "Enter Test Case ID")
                {
                    MessageBox.Show("Please enter a valid Test Case ID (e.g., TC00011).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string? scriptType = (cmbScriptType.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(scriptType) || (scriptType != "playwright" && scriptType != "selenium"))
                {
                    MessageBox.Show("Please select a valid script type (playwright or selenium).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                btnExecute.IsEnabled = false;
                _logs.Clear();
                lvExecutionLogs.ItemsSource = null;
                lvExecutionLogs.ItemsSource = _logs;

                _cancellationTokenSource = new CancellationTokenSource(WEBSOCKET_TIMEOUT_MS);
                _webSocket = new ClientWebSocket();
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_token}");

                var baseUrl = _apiClient.BaseUrl?.TrimEnd('/') ?? "http://127.0.0.1:8002";
                string wsScheme = baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
                var hostPart = baseUrl.Replace("http://", "").Replace("https://", "");
                string wsUrl = $"{wsScheme}://{hostPart}/testcases/{Uri.EscapeDataString(testCaseId)}/execute-ws?script_type={Uri.EscapeDataString(scriptType)}";

                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Connecting to {wsUrl}", status = "INFO" });
                lvExecutionLogs.Items.Refresh();

                await _webSocket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);

                var tokenMessage = JsonSerializer.Serialize(new { token = _token });
                var tokenBuffer = Encoding.UTF8.GetBytes(tokenMessage);
                await _webSocket.SendAsync(new ArraySegment<byte>(tokenBuffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);

                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "WebSocket connected and token sent", status = "INFO" });
                lvExecutionLogs.Items.Refresh();

                _ = Task.Run(ReceiveWebSocketMessages);
            }
            catch (OperationCanceledException)
            {
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "WebSocket connection timed out. Server may be unresponsive.", status = "ERROR" });
                lvExecutionLogs.Items.Refresh();
                MessageBox.Show("WebSocket connection timed out. Please ensure the server is running and responsive.", "Timeout", MessageBoxButton.OK, MessageBoxImage.Error);
                btnExecute.IsEnabled = true;
                CloseWebSocket();
            }
            catch (WebSocketException wex)
            {
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"WebSocket connection failed: {wex.Message}", status = "ERROR" });
                lvExecutionLogs.Items.Refresh();
                MessageBox.Show($"WebSocket connection failed: {wex.Message}\nEnsure the server is running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                btnExecute.IsEnabled = true;
                CloseWebSocket();
            }
            catch (Exception ex)
            {
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Error initiating execution: {ex.Message}", status = "ERROR" });
                lvExecutionLogs.Items.Refresh();
                MessageBox.Show($"Error initiating execution: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                btnExecute.IsEnabled = true;
                CloseWebSocket();
            }
        }

        private async Task ReceiveWebSocketMessages()
        {
            try
            {
                var buffer = new byte[1024 * 4];
                while (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        try
                        {
                            Dispatcher.Invoke(() =>
                            {
                                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Raw message received: {message}", status = "DEBUG" });
                                lvExecutionLogs.Items.Refresh();
                            });

                            var data = JsonSerializer.Deserialize<ExecutionResponse>(message);

                            Dispatcher.Invoke(() =>
                            {
                                if (data == null)
                                {
                                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "Received empty or invalid execution response", status = "ERROR" });
                                    lvExecutionLogs.Items.Refresh();
                                    btnExecute.IsEnabled = true;
                                    CloseWebSocket();
                                    return;
                                }

                                bool hasError = data.logs != null && Array.Exists(data.logs, log => string.Equals(log.status, "ERROR", StringComparison.OrdinalIgnoreCase));
                                if (hasError)
                                {
                                    if (data.logs != null)
                                    {
                                        foreach (var log in data.logs)
                                        {
                                            _logs.Add(log);
                                        }
                                    }
                                    lvExecutionLogs.Items.Refresh();
                                    CloseWebSocket();
                                }
                                else
                                {
                                    if (data.logs != null)
                                    {
                                        foreach (var log in data.logs)
                                        {
                                            _logs.Add(log);
                                        }
                                        lvExecutionLogs.Items.Refresh();
                                    }

                                    if (string.Equals(data.status, "COMPLETED", StringComparison.OrdinalIgnoreCase) || string.Equals(data.status, "FAILED", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Execution {data.status?.ToLowerInvariant()}", status = data.status ?? "INFO" });
                                        lvExecutionLogs.Items.Refresh();
                                        CloseWebSocket();
                                    }
                                }

                                bool socketOpen = _webSocket != null && _webSocket.State == WebSocketState.Open;
                                btnExecute.IsEnabled = !socketOpen;
                            });
                        }
                        catch (JsonException jex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"JSON parsing error: {jex.Message}", status = "ERROR" });
                                lvExecutionLogs.Items.Refresh();
                                btnExecute.IsEnabled = true;
                                CloseWebSocket();
                            });
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "WebSocket connection closed by server", status = "INFO" });
                            lvExecutionLogs.Items.Refresh();
                            btnExecute.IsEnabled = true;
                            CloseWebSocket();
                        });
                        break;
                    }
                }
            }
            catch (WebSocketException wex)
            {
                Dispatcher.Invoke(() =>
                {
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"WebSocket error: {wex.Message}", status = "ERROR" });
                    lvExecutionLogs.Items.Refresh();
                    btnExecute.IsEnabled = true;
                    CloseWebSocket();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Unexpected error: {ex.Message}", status = "ERROR" });
                    lvExecutionLogs.Items.Refresh();
                    btnExecute.IsEnabled = true;
                    CloseWebSocket();
                });
            }
        }

        private void CloseWebSocket()
        {
            try
            {
                if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting))
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None).GetAwaiter().GetResult();
                    _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = "WebSocket closed by client", status = "INFO" });
                    lvExecutionLogs.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                _logs.Add(new ExecutionLogEntry { timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), message = $"Error closing WebSocket: {ex.Message}", status = "ERROR" });
                lvExecutionLogs.Items.Refresh();
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                Dispatcher.Invoke(() => btnExecute.IsEnabled = true);
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            CloseWebSocket();
            var window = Window.GetWindow(this);
            if (window is DashboardWindow dashboard)
            {
                dashboard.contentArea.Content = new ExecutionLogPage(_token);
            }
        }

        public void Dispose()
        {
            CloseWebSocket();
            _cancellationTokenSource?.Dispose();
        }
    }
}
