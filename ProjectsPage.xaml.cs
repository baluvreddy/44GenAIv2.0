using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyWPFApp
{
    public partial class ProjectsPage : UserControl
    {
        private readonly ApiClient _apiClient;
        private readonly string _token;
        private readonly Action<UserControl> _navigateTo;

        public ProjectsPage(string token, Action<UserControl> navigateTo)
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _token = token;
            _apiClient.SetBearer(_token);
            _navigateTo = navigateTo;
            LoadProjects();
        }

        private async void LoadProjects()
        {
            try
            {
                var response = await _apiClient.GetAsync("GetProjects");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var projects = JsonSerializer.Deserialize<List<Project>>(jsonResponse);

                    ProjectsWrapPanel.Children.Clear();
                    foreach (var project in projects)
                    {
                        var card = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(5),
                            Margin = new Thickness(10),
                            Padding = new Thickness(10),
                            Width = 200,
                            Height = 150
                        };
                        var stackPanel = new StackPanel();
                        stackPanel.Children.Add(new TextBlock { Text = project.title, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0, 0, 0, 5) });
                        stackPanel.Children.Add(new TextBlock { Text = $"ID: {project.projectid}", FontSize = 12 });
                        stackPanel.Children.Add(new TextBlock { Text = $"Type: {project.projecttype}", FontSize = 12 });
                        stackPanel.Children.Add(new TextBlock { Text = $"Start: {project.startdate}", FontSize = 12 });
                        stackPanel.Children.Add(new TextBlock { Text = project.description, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                        var viewTestCasesButton = new Button
                        {
                            Content = "View Test Cases",
                            Width = 120,
                            Height = 25,
                            Margin = new Thickness(0, 5, 0, 0),
                            Tag = project.projectid
                        };
                        viewTestCasesButton.Click += (s, e) =>
                        {
                            _navigateTo(new TestCasesPage(_token, project.projectid, project.title, _navigateTo));
                        };
                        stackPanel.Children.Add(viewTestCasesButton);
                        card.Child = stackPanel;
                        ProjectsWrapPanel.Children.Add(card);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = "Failed to load projects.";
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
                MessageBox.Show($"Error loading projects: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCreateProject_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to create project page. When creation completes we re-open the projects page.
            _navigateTo(new CreateProjectPage(_token, () => _navigateTo(new ProjectsPage(_token, _navigateTo))));
        }
    }
}