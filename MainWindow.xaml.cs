using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.Text.Json;
using System.Windows.Media;

namespace LanguageMagic
{
    public class AppSettings
    {
        public bool IsActivated { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private LanguageSwitcher? switcher = null;
        private bool isRunning = false;
        private NotifyIcon trayIcon;

        private readonly SolidColorBrush activeBrush =
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#23d937"));
        private readonly SolidColorBrush inactiveBrush =
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#808080"));

        private readonly string configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LanguageMagic", "config.json"
        );

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings(); // טוען את מצב ההפעלה הקודם

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };
            trayIcon.ContextMenuStrip.Items.Add("Open", null, (s, e) => ShowWindow());
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => ExitApplication());

            trayIcon.Click += (s, e) => ShowWindow();

            if (isRunning)
            {
                if (switcher == null)
                    switcher = new LanguageSwitcher();

                switcher.Start();
                ActivateBtn.Background = activeBrush;
                StatusText.Text = "Active";
            }

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "/autostart")
            {
                this.Hide();
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            switcher?.Stop();
            SaveSettings();

            isRunning = false;
            trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning)
            {
                if (switcher == null)
                {
                    switcher = new LanguageSwitcher();
                }
                isRunning = true;
                switcher.Start();
                ActivateBtn.Background = activeBrush;
                if (StatusText != null) StatusText.Text = "Active";
            }
            else
            {
                switcher?.Stop();
                isRunning = false;
                ActivateBtn.Background = inactiveBrush;
                if (StatusText != null) StatusText.Text = "Inactive";
            }

            SaveSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    isRunning = settings?.IsActivated ?? false;
                    System.Diagnostics.Debug.WriteLine("Loaded activation state: " + isRunning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error loading settings: " + ex.Message);
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings { IsActivated = isRunning };
                string json = JsonSerializer.Serialize(settings);

                string folder = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }
    }
}
