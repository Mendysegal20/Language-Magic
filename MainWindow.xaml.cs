using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;

namespace LanguageMagic
{
    public partial class MainWindow : Window
    {
        private LanguageSwitcher? switcher = null;
        private bool isRunning = false;
        private NotifyIcon trayIcon;
        private const string appName = "LanguageMagic";
        private readonly string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

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

            // אם התוכנית הופעלה מחדש כשהיא הייתה מופעלת קודם, נוודא שהיא מתחילה לפעול
            if (isRunning)
            {
                if (switcher == null)
                    switcher = new LanguageSwitcher();

                switcher.Start();
                ActivateBtn.Content = "Deactivate";
            }

            // אם הופעלה עם פרמטר /autostart, מסתירים את החלון
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
            // רק שומר את מצב ההפעלה, לא משנה את מצב האתחול
            SaveActivationState();

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
                ActivateBtn.Content = "Deactivate";
            }
            else
            {
                switcher?.Stop();
                isRunning = false;
                ActivateBtn.Content = "Activate";
            }

            // כאן אנחנו גם שומרים את המצב וגם משנים את האתחול
            SaveSettings();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        // פונקציה חדשה שרק שומרת את מצב ההפעלה
        private void SaveActivationState()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\LanguageMagic"))
                {
                    key.SetValue("IsActivated", isRunning ? 1 : 0);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving activation state: " + ex.Message);
            }
        }

        // שמירת הגדרת ההפעלה ברג'יסטרי וגם שינוי מצב האתחול
        private void SaveSettings()
        {
            try
            {
                // שמירת מצב ההפעלה ברג'יסטרי
                SaveActivationState();

                // הוספה או הסרה מרשימת האתחול בהתאם למצב ההפעלה
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (isRunning)
                    {
                        // הוספה לרשימת האתחול עם פרמטר /autostart
                        key.SetValue(appName, $"\"{exePath}\" /autostart");
                    }
                    else
                    {
                        // הסרה מרשימת האתחול
                        if (key.GetValue(appName) != null)
                            key.DeleteValue(appName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }

        // טעינת ההגדרות מהרשומות
        private void LoadSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\LanguageMagic"))
                {
                    if (key != null)
                    {
                        
                        int value = (int)key.GetValue("IsActivated", 0);
                        System.Diagnostics.Debug.WriteLine("the value of activation is: " + value);
                        isRunning = value == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error loading settings: " + ex.Message);
            }
        }
    }
}