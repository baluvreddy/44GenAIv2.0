using System.Windows;
using System.Windows.Controls;
using System.Threading;

namespace MyWPFApp
{
    public partial class DashboardWindow : Window
    {
        private readonly LoginResponse _loginData;

        public CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public DashboardWindow(LoginResponse loginData)
        {
            InitializeComponent();
            _loginData = loginData;
            txtUsername.Text = _loginData.userid;
            btnProjects_Click(this, new RoutedEventArgs());
        }

        private void btnProjects_Click(object sender, RoutedEventArgs e)
        {
            contentArea.Content = new ProjectsPage(_loginData.token, content => contentArea.Content = content);
        }

        private void btnTestExecution_Click(object sender, RoutedEventArgs e)
        {
            contentArea.Content = new TestExecutionPage(_loginData.token);
        }

        private void btnGenerateScript_Click(object sender, RoutedEventArgs e)
        {
            contentArea.Content = new GenerateScriptPage(_loginData.token);
        }

        private void btnReuseScript_Click(object sender, RoutedEventArgs e)
        {
            contentArea.Content = new TextBlock { Text = "Reuse Script Page (To be implemented)", FontSize = 20, Margin = new Thickness(20) };
        }

        private void btnExecutionLog_Click(object sender, RoutedEventArgs e)
        {
            contentArea.Content = new ExecutionLogPage(_loginData.token);
        }

        private void btnUserMenu_Click(object sender, RoutedEventArgs e)
        {
            popUserMenu.IsOpen = true;
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}