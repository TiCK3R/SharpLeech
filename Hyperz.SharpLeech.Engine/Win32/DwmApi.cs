using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Hyperz.SharpLeech.Engine.Win32
{
	public sealed class DwmApi
	{
		[DllImport("DwmApi.dll")]
		private static extern int DwmEnableComposition(int uCompositionAction);
		[DllImport("DwmApi.dll")]
		private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref DwmMargins pMarInset);

		private static readonly int DWM_EC_DISABLECOMPOSITION = 0;
		private static readonly int DWM_EC_ENABLECOMPOSITION = 1;

		public static int EnableComposition(bool enable)
		{
			try
			{
				return DwmEnableComposition(enable ? DWM_EC_ENABLECOMPOSITION : DWM_EC_DISABLECOMPOSITION);
			}
			catch (DllNotFoundException)
			{
				return -1;
			}
		}

		public static int ExtendFrameIntoClientArea(Window window, int leftWidth, int rightWidth, int topHeight, int bottomHeight)
		{
			try
			{
				// Get the window handle
				IntPtr mainWindowPtr = new WindowInteropHelper(window).Handle;
				HwndSource mainWindowSrc = HwndSource.FromHwnd(mainWindowPtr);
				mainWindowSrc.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

				// Get DPI
				using (var desktop = System.Drawing.Graphics.FromHwnd(mainWindowPtr))
				{
					float DesktopDpiX = desktop.DpiX / 96;
					float DesktopDpiY = desktop.DpiY / 96;

					// Set Margins
					DwmMargins margins = new DwmMargins();

					margins.xLeft = (int)(leftWidth * DesktopDpiX);
					margins.xRight = (int)(rightWidth * DesktopDpiX);
					margins.yTop = (int)(topHeight * DesktopDpiY);
					margins.yBottom = (int)(bottomHeight * DesktopDpiY);

					return DwmExtendFrameIntoClientArea(mainWindowSrc.Handle, ref margins);
				}
			}
			catch (DllNotFoundException)
			{
				return -1;
			}
		}

		public static int ExtendGlassFrame(Window window, int all = -1)
		{
			return ExtendFrameIntoClientArea(window, all, all, all, all);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DwmMargins
	{
		public int xLeft;
		public int xRight;
		public int yTop;
		public int yBottom;
	};
}
