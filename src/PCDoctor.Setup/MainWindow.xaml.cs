using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace PCDoctor.Setup;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void InstallButton_OnClick(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        Progress.Value = 15;
        StatusText.Text = "Masaüstüne kopyalanıyor...";

        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var destination = Path.Combine(desktop, "Tondy Pc Doctor.exe");

            await using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe");
            if (source is null)
            {
                throw new InvalidOperationException("Kurulum paketi eksik. Setup yeniden oluşturulmalı.");
            }

            Progress.Value = 55;
            await using var target = File.Create(destination);
            await source.CopyToAsync(target).ConfigureAwait(true);
            Progress.Value = 100;

            StatusText.Text = "Kurulum tamam.";
            InstallButton.Content = "Çalıştır";
            InstallButton.IsEnabled = true;
            InstallButton.Click -= InstallButton_OnClick;
            InstallButton.Click += (_, _) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = destination,
                    UseShellExecute = true
                });
                Close();
            };
        }
        catch (Exception ex)
        {
            Progress.Value = 0;
            StatusText.Text = "Kurulum başarısız: " + ex.Message;
            InstallButton.IsEnabled = true;
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
