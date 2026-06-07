using System.Windows;

namespace SmartMeetingNotes.Desktop;

public partial class MainWindow : Window
{
    private readonly string _apiUrl;

    public MainWindow(string apiUrl)
    {
        _apiUrl = apiUrl;
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.Navigate(_apiUrl);
    }
}
