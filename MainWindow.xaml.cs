using System;
using System.Windows;
using System.Windows.Forms; // צריך להוסיף Reference ל-System.Windows.Forms
using System.Drawing;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;

namespace LanguageMagic
{
    public partial class MainWindow : Window
    {
        private LanguageSwitcher? switcher = null;
        private bool isRunning = false;
        private NotifyIcon trayIcon;

        public MainWindow()
        {
            InitializeComponent();

            // יצירת תפריט ההקשר
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            // יצירת אייקון במגש המערכת
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // קובץ אייקון מתאים
                Visible = true,
                ContextMenuStrip = contextMenu // שימוש ב-ContextMenuStrip במקום ContextMenu
            };

            trayIcon.Click += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            this.Show(); // מראה את החלון
            this.WindowState = WindowState.Normal; // אם החלון היה ממוזער, מחזיר אותו למצב רגיל
            this.Activate(); // מבטיח שהחלון יקבל פוקוס
        }

        private void ExitApplication()
        {
            switcher?.Stop();
            isRunning = false;
            trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // מונע סגירה
            this.Hide(); // מסתיר את החלון במקום לסגור אותו
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;

            if (!isRunning)
            {
                if (switcher == null)
                {
                    switcher = new LanguageSwitcher();
                }
                isRunning = true;
                switcher.Start();
                btn.Content = "Deactivate";
            }
            else
            {
                switcher?.Stop();
                isRunning = false;
                btn.Content = "Activate";
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }
    }
}
