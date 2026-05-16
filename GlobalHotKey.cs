using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinSpotlight
{
    public class GlobalHotKey : IDisposable
    {
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private int _id;
        private IntPtr _hWnd;
        private Action _onHotKeyPressed;

        public GlobalHotKey(IntPtr hWnd, int id, uint modifiers, uint key, Action onHotKeyPressed)
        {
            _hWnd = hWnd;
            _id = id;
            _onHotKeyPressed = onHotKeyPressed;

            if (!RegisterHotKey(_hWnd, _id, modifiers, key))
            {
                // Failed to register hotkey
                Console.WriteLine("Warning: Hotkey Alt+C might be in use.");
            }

            ComponentDispatcher.ThreadPreprocessMessage += ThreadPreprocessMessageMethod;
        }

        private void ThreadPreprocessMessageMethod(ref MSG msg, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (!handled && msg.message == WM_HOTKEY && (int)msg.wParam == _id)
            {
                _onHotKeyPressed?.Invoke();
                handled = true;
            }
        }

        public void Dispose()
        {
            ComponentDispatcher.ThreadPreprocessMessage -= ThreadPreprocessMessageMethod;
            UnregisterHotKey(_hWnd, _id);
        }
    }
}
