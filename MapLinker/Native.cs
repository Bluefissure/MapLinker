using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Logging;

namespace MapLinker
{
    class Native
    {
        #region Enums and Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public FlashWindow dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        public enum FlashWindow : uint
        {
            /// <summary>
            /// Stop flashing. The system restores the window to its original state.
            /// </summary>    
            FLASHW_STOP = 0,

            /// <summary>
            /// Flash the window caption
            /// </summary>
            FLASHW_CAPTION = 1,

            /// <summary>
            /// Flash the taskbar button.
            /// </summary>
            FLASHW_TRAY = 2,

            /// <summary>
            /// Flash both the window caption and taskbar button.
            /// This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
            /// </summary>
            FLASHW_ALL = 3,

            /// <summary>
            /// Flash continuously, until the FLASHW_STOP flag is set.
            /// </summary>
            FLASHW_TIMER = 4,

            /// <summary>
            /// Flash continuously until the window comes to the foreground.
            /// </summary>
            FLASHW_TIMERNOFG = 12
        }

        [Flags]
        public enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }

        #endregion

        const int GWL_STYLE = (-16);
        const int WS_MINIMIZE = 0x20000000;
        const int SW_SHOW = 0x05;
        const int SW_RESTORE = 0x09;
        const int SW_MINIMIZE = 0x06;

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            int activeProcId;
            GetWindowThreadProcessId(activatedHandle, out activeProcId);

            return activeProcId == procId;
        }

        //BOOL GetOpenFileName(LPOPENFILENAME lpofn);

        //[DllImport("Comdlg32.dll", CharSet = CharSet.Auto)]
        //internal static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        public class Impl
        {

            public static void FlashWindow()
            {
                if (TryFindGameWindow(out var hwnd))
                {
                    PluginLog.Debug($"FlashWindow begins with hwid={hwnd:X16}");
                    var flashInfo = new Native.FLASHWINFO
                    {
                        cbSize = (uint)Marshal.SizeOf<Native.FLASHWINFO>(),
                        uCount = uint.MaxValue,
                        dwTimeout = 0,
                        dwFlags = Native.FlashWindow.FLASHW_ALL |
                                                    Native.FlashWindow.FLASHW_TIMERNOFG,
                        hwnd = hwnd
                    };
                    FlashWindowEx(ref flashInfo);
                }
                else
                {
                    PluginLog.Error("Failed to find FFXIV window, somehow.");
                }
            }


            public static void Activate()
            {
                PluginLog.Information("Bringing FFXIV foreground.");
                if (TryFindGameWindow(out var focusOnWindowHandle))
                {
                    if (IsIconic(focusOnWindowHandle))
                    {
                        PluginLog.Information("Window is minimized.");
                        ShowWindow(focusOnWindowHandle, SW_RESTORE);
                        if (ApplicationIsActivated())
                        {
                            PluginLog.Information("Success: FFXIV brought to front.");
                            return;
                        }
                    }
                    PluginLog.Information($"SetForegroundWindow: {SetForegroundWindow(focusOnWindowHandle)}");
                    if (ApplicationIsActivated())
                    {
                        PluginLog.Information("Success: FFXIV brought to front.");
                        return;
                    }
                    else
                    {
                        PluginLog.Information("Failed to bring FFXIV to front. Trying minimize + restore...");
                        ShowWindow(focusOnWindowHandle, SW_MINIMIZE);
                        ShowWindow(focusOnWindowHandle, SW_RESTORE);
                        if (ApplicationIsActivated())
                        {
                            PluginLog.Information("Success: FFXIV brought to front.");
                            return;
                        }
                        else
                        {
                            PluginLog.Information("Failed to bring FFXIV to front.");
                        }
                    }
                }
                else
                {
                    PluginLog.Information("Failed to find FFXIV, as funny as it may sound.");
                }
            }

            public static bool TryFindGameWindow(out IntPtr hwnd)
            {
                hwnd = IntPtr.Zero;
                while (true)
                {
                    hwnd = FindWindowEx(IntPtr.Zero, hwnd, "FFXIVGAME", null);
                    if (hwnd == IntPtr.Zero) break;
                    GetWindowThreadProcessId(hwnd, out var pid);
                    if (pid == Process.GetCurrentProcess().Id) break;
                }
                return hwnd != IntPtr.Zero;
            }
        }
    }
}