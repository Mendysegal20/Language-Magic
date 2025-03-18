using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

class LanguageSwitcher
{

    //
    private bool isRunning = false;
    private Task? trackingTask;
    private CancellationTokenSource? cts;
    //

    #region Win32 API
    [StructLayout(LayoutKind.Sequential)]
    struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int left, top, right, bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    private const int INPUTLANGCHANGE_FORWARD = 0x0002;
    #endregion

    private static Dictionary<string, WindowPreference> windowPreferences = new Dictionary<string, WindowPreference>();
    private static string currentWindowId = string.Empty;
    private static int currentLanguageId = 0;
    private static bool running = true;

    // Hebrew language ID (0x040D)
    private const int HEBREW_LANGUAGE_ID = 0x040D;

    class WindowPreference
    {
        public string? WindowId { get; set; }
        public string? Title { get; set; }
        public int LanguageId { get; set; }
    }

    public void Start()
    {
        Console.WriteLine("Automatic Language Switcher started...");
        Console.WriteLine("The program is now running in the background.");
        Console.WriteLine("Press Ctrl+C to exit.");

        //
        if (isRunning) return; // אם כבר רץ, לא לעשות כלום

        isRunning = true;
        running = true;
        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        trackingTask = Task.Run(() => TrackLanguage(token), token);
        //

        //Console.CancelKeyPress += (sender, e) => {
        //    Console.WriteLine("Exiting...");
        //    running = false;
        //    e.Cancel = true;
        //};

        //// Start monitoring loop
        //MonitorLanguage();
    }

    public void Stop()
    {
        if (!isRunning) return; // אם כבר נעצר, לא לעשות כלום

        isRunning = false;
        running = false; // עצירה מוחלטת של הלולאה ב-MonitorLanguage
        cts?.Cancel(); // מבטל את המשימה
    }


    //
    private async Task TrackLanguage(CancellationToken token)
    {
        Console.WriteLine("Tracking started...");

        while (!token.IsCancellationRequested)
        {
            await MonitorLanguage(token);
            await Task.Delay(500, token); // מחכה חצי שנייה בין בדיקות
        }

        Console.WriteLine("Tracking stopped.");
    }

    private async Task MonitorLanguage(CancellationToken token)
    {
        try
        {
            GUITHREADINFO guiThreadInfo = new GUITHREADINFO
            {
                cbSize = Marshal.SizeOf(typeof(GUITHREADINFO))
            };

            if (GetGUIThreadInfo(0, ref guiThreadInfo))
            {
                IntPtr focusWindow = guiThreadInfo.hwndFocus;
                IntPtr activeWindow = guiThreadInfo.hwndActive;

                if (focusWindow != IntPtr.Zero)
                {
                    string windowTitle = GetWindowTitle(activeWindow);
                    if (string.IsNullOrEmpty(windowTitle))
                    {
                        windowTitle = "Untitled Window";
                    }

                    string windowId = $"{windowTitle}_{activeWindow.ToInt64()}";

                    uint threadId = GetWindowThreadProcessId(focusWindow, out _);
                    if (threadId != 0)
                    {
                        IntPtr keyboardLayout = GetKeyboardLayout(threadId);
                        int languageId = keyboardLayout.ToInt32() & 0xFFFF;

                        if (windowId != currentWindowId)
                        {
                            Console.WriteLine($"Window changed to: {windowTitle} (ID: {windowId})");
                            currentWindowId = windowId;

                            if (windowPreferences.TryGetValue(windowId, out WindowPreference? preference))
                            {
                                if (languageId != preference.LanguageId)
                                {
                                    ChangeKeyboardLayout(activeWindow, threadId, (IntPtr)((preference.LanguageId & 0xFFFF) | (languageId & 0xFFFF0000)));
                                    Console.WriteLine($"Changed language in window: {windowTitle} to: {GetLanguageName(preference.LanguageId)}");
                                }
                            }
                            else
                            {
                                windowPreferences[windowId] = new WindowPreference
                                {
                                    WindowId = windowId,
                                    Title = windowTitle,
                                    LanguageId = languageId
                                };

                                Console.WriteLine($"New window detected: {windowTitle}, keeping current language: {GetLanguageName(languageId)}");
                                currentLanguageId = languageId;
                            }
                        }
                        else if (languageId != currentLanguageId)
                        {
                            currentLanguageId = languageId;
                            windowPreferences[windowId] = new WindowPreference
                            {
                                WindowId = windowId,
                                Title = windowTitle,
                                LanguageId = languageId
                            };

                            Console.WriteLine($"Saved new language preference for window: {windowTitle}, language: {GetLanguageName(languageId)}");
                        }

                        currentLanguageId = languageId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length > 0)
        {
            var builder = new System.Text.StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }
        return string.Empty;
    }

    static string GetLanguageName(int languageId)
    {
        return languageId switch
        {
            0x0409 => "English",
            0x040D => "Hebrew",
            _ => $"Other (0x{languageId:X4})"
        };
    }

    static void ChangeKeyboardLayout(IntPtr hWnd, uint threadId, IntPtr layout)
    {
        PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, (IntPtr)INPUTLANGCHANGE_FORWARD, layout);
    }
}