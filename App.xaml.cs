using System;
using System.Windows;

namespace MyWPFApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public ApiClient ApiClient { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ApiClient = new ApiClient();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ApiClient?.Dispose();
            base.OnExit(e);
        }
    }
}
