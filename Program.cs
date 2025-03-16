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
        public string WindowId { get; set; }
        public string Title { get; set; }
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
    private void TrackLanguage(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // כאן נכניס את הקוד שעוקב אחר השפה
            MonitorLanguage();
            Console.WriteLine("מעקב פעיל...");

            Thread.Sleep(500); // מחכה חצי שנייה כדי לא להעמיס על המעבד
        }

        Console.WriteLine("המעקב הופסק.");
    }


    static void MonitorLanguage()
    {
        while (running)
        {
            try
            {
                // Get focused window

                // new struct of window with all details
                GUITHREADINFO guiThreadInfo = new GUITHREADINFO();
                
                // returns the number of bytes of the struct in memory
                guiThreadInfo.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));

                // 0 - allows us to get an information about the current thread
                // if we found an active window, guiThreadInfo woud fill with all details
                if (GetGUIThreadInfo(0, ref guiThreadInfo))
                {
                    IntPtr focusWindow = guiThreadInfo.hwndFocus;
                    IntPtr activeWindow = guiThreadInfo.hwndActive;

                    if (focusWindow != IntPtr.Zero)
                    {
                        // Get window title
                        string windowTitle = GetWindowTitle(activeWindow);
                        if (string.IsNullOrEmpty(windowTitle))
                        {
                            windowTitle = "Untitled Window";
                        }

                        // Create a unique window ID using both title and window handle value
                        string windowId = $"{windowTitle}_{activeWindow.ToInt64()}";

                        // Get keyboard layout
                        uint threadId = GetWindowThreadProcessId(focusWindow, out _);
                        if (threadId != 0)
                        {
                            IntPtr keyboardLayout = GetKeyboardLayout(threadId);
                            int languageId = keyboardLayout.ToInt32() & 0xFFFF;

                            // When we've moved to a new window
                            if (windowId != currentWindowId)
                            {
                                Console.WriteLine($"Window changed to: {windowTitle} (ID: {windowId})");

                                // Save current window
                                currentWindowId = windowId;

                                // If we have saved preferences for this window, switch to that language
                                if (windowPreferences.TryGetValue(windowId, out WindowPreference preference))
                                {
                                    if (languageId != preference.LanguageId)
                                    {
                                        // switch to the saved language
                                        ChangeKeyboardLayout(activeWindow, threadId, (IntPtr)((preference.LanguageId & 0xFFFF) | (languageId & 0xFFFF0000)));
                                        Console.WriteLine($"Changed language in window: {windowTitle} to: {GetLanguageName(preference.LanguageId)}");
                                    }
                                }
                                // V2 - no default language
                                else
                                {
                                    // Switch to Hebrew
                                    windowPreferences[windowId] = new WindowPreference
                                    {
                                        WindowId = windowId,
                                        Title = windowTitle,
                                        LanguageId = languageId // שומר את השפה הנוכחית של החלון במקום לשנות אותה
                                    };

                                    Console.WriteLine($"New window detected: {windowTitle}, keeping current language: {GetLanguageName(languageId)}");

                                    // עדכון מזהה השפה הנוכחי
                                    currentLanguageId = languageId;
                                }
                                //// If this is a new window, set default to Hebrew
                                //// V1 - default language as hebrew
                                //else
                                //{
                                //    // Switch to Hebrew
                                //    if (languageId != HEBREW_LANGUAGE_ID)
                                //    {
                                //        ChangeKeyboardLayout(activeWindow, threadId, (IntPtr)((HEBREW_LANGUAGE_ID & 0xFFFF) | (languageId & 0xFFFF0000)));
                                //        Console.WriteLine($"New window detected: {windowTitle}, setting default language to Hebrew");

                                //        // Save this preference
                                //        windowPreferences[windowId] = new WindowPreference
                                //        {
                                //            WindowId = windowId,
                                //            Title = windowTitle,
                                //            LanguageId = HEBREW_LANGUAGE_ID
                                //        };

                                //        // Update current language ID
                                //        currentLanguageId = HEBREW_LANGUAGE_ID;
                                //    }
                                //    else
                                //    {
                                //        // Window is already in Hebrew, just save the preference
                                //        windowPreferences[windowId] = new WindowPreference
                                //        {
                                //            WindowId = windowId,
                                //            Title = windowTitle,
                                //            LanguageId = HEBREW_LANGUAGE_ID
                                //        };
                                //        currentLanguageId = HEBREW_LANGUAGE_ID;
                                //    }

                                //}

                            }
                            // If language changed in the current window, save the new preference
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

            Thread.Sleep(500);
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