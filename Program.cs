using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace WindowsFormsApp1
{
	public static class KeyboardHook
	{
		public static event EventHandler<KeyPressedEventArgs> KeyPressed;
		public static void Start()
		{
			_hookID = SetHook(_proc);
		}
		public static void Stop()
		{
			UnhookWindowsHookEx(_hookID);
		}
		private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
		private static IntPtr SetHook(LowLevelKeyboardProc proc)
		{
			using (Process curProcess = Process.GetCurrentProcess())
			using (ProcessModule curModule = curProcess.MainModule)
			{
				return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
			}
		}
		private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
			{
				int vkCode = Marshal.ReadInt32(lParam);
				Keys key = (Keys)vkCode;
				KeyPressed?.Invoke(null, new KeyPressedEventArgs(key));
			}
			return CallNextHookEx(_hookID, nCode, wParam, lParam);
		}
		private const int WH_KEYBOARD_LL = 13;
		private const int WM_KEYDOWN = 0x0100;
		private static LowLevelKeyboardProc _proc = HookCallback;
		private static IntPtr _hookID = IntPtr.Zero;
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);
	}
	public class KeyPressedEventArgs : EventArgs
	{
		public Keys Key { get; private set; }
		public KeyPressedEventArgs(Keys key)
		{
			Key = key;
		}
	}
	public static class Startup
	{
		public static void ToggleRunOnStartup(object sender)
		{
			MenuItem menuItem = (MenuItem)sender;
			menuItem.Checked = !menuItem.Checked;
			menuItem.Text = menuItem.Checked ? "Don't run on startup" : "Run on startup";
			RegistryKey rkApp = Registry.CurrentUser.OpenSubKey
				("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			if (rkApp.GetValue("ColorPicker") == null)
			{
				rkApp.SetValue("ColorPicker", Application.ExecutablePath);
			}
			else
			{
				rkApp.DeleteValue("ColorPicker", false);
			}
		}
	}
	public static class PixelGetter
	{
		[DllImport("user32.dll")]
		public static extern IntPtr GetDC(IntPtr hwnd);
		[DllImport("user32.dll")]
		public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
		[DllImport("gdi32.dll")]
		public static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
		public static uint GetColorAt(Point location)
		{
			IntPtr hdc = GetDC(IntPtr.Zero);
			uint color = GetPixel(hdc, location.X, location.Y);
			ReleaseDC(IntPtr.Zero, hdc);
			return color;
		}
	}
	internal static class Program
	{
		[DllImport("user32.dll")]
		public static extern short GetAsyncKeyState(Keys vKey);
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			NotifyIcon notifyIcon = new NotifyIcon();
			notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			notifyIcon.Visible = true;
			notifyIcon.Text = "ColorPicker";
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.MenuItems.Add("Run on startup", (sender, args) => Startup.ToggleRunOnStartup(sender));
			contextMenu.MenuItems.Add("Exit", (sender, args) => Application.Exit());
			notifyIcon.ContextMenu = contextMenu;
			KeyboardHook.KeyPressed += (sender, args) =>
			{
				if (args.Key == Keys.Pause)
				{
					notifyIcon.ShowBalloonTip(1000, "ColorPicker", "Click anywhere to copy the color to clipboard", ToolTipIcon.Info);
					while (true)
					{
						if (GetAsyncKeyState(Keys.LButton) != 0)
						{
							Point cursor = Cursor.Position;
							uint color = PixelGetter.GetColorAt(cursor);
							string hex = color.ToString("X6");
							Clipboard.SetText(hex);
							notifyIcon.ShowBalloonTip(1000, "ColorPicker", $"Color {hex} copied to clipboard", ToolTipIcon.Info);
							break;
						}
					}
				}
			};
			KeyboardHook.Start();
			Application.Run();
		}
	}
}