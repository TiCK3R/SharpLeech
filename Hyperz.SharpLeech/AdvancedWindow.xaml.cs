using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Microsoft.Win32;

using Hyperz.SharpLeech.Engine.Win32;
using Hyperz.SharpLeech.Engine.Wpf;

using Res = Hyperz.SharpLeech.Properties.Resources;
using Cfg = Hyperz.SharpLeech.Properties.Settings;

namespace Hyperz.SharpLeech
{
	/// <summary>
	/// Interaction logic for AdvancedWindow.xaml
	/// </summary>
	public partial class AdvancedWindow : Window, IGlassWindow
	{
		public bool IsNicknameChanged { get; private set; }
		public bool IsRestartRequired { get; private set; }

		public AdvancedWindow()
		{
			InitializeComponent();

			// Fix black background upon theme change
			SystemEvents.UserPreferenceChanged += delegate { this.TryExtendAeroGlass(); };
		}

		public bool? TryExtendAeroGlass()
		{
			Brush color = this.Background;

			if (!Cfg.Default.EnableAeroInterface)
			{
				this.Background = color;
				return false;
			}

			this.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

			// Extend Aero glass into client area
			int result = DwmApi.ExtendFrameIntoClientArea(this, -1, -1, -1, -1);

			if (result < 0)
			{
				this.Background = color;
				return false;
			}
			else
			{
				return true;
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// Setup some controls
			this.txtMaxLeechPages.TextChanged += MainWindow.TextChangedInt32Filter;
			this.lnkRegex.Click += (_s, _e) => Process.Start("http://en.wikipedia.org/wiki/Regular_expression");
			this.lnkTool.Click += (_s, _e) => Process.Start("http://www.regexbuddy.com/screen.html");
			this.lnkTutorial.Click += (_s, _e) => Process.Start("http://www.codeproject.com/KB/dotnet/regextutorial.aspx");
			
			// Setup some stuff
			this.IsNicknameChanged = false;
			this.IsRestartRequired = false;
			
			// Extend Aero glass into client area
			this.TryExtendAeroGlass();

			// Save so we can cancel (=reload) changes later
			Cfg.Default.Save();
		}

		private void btnRestoreDefaults_Click(object sender, RoutedEventArgs e)
		{
			MessageBoxResult result = MessageBox.Show(
				Res.MsgResetSettings,
				"Confirmation",
				MessageBoxButton.YesNo,
				MessageBoxImage.Exclamation
			);

			if (result == MessageBoxResult.Yes)
			{
				Cfg.Default.Reset();
				this.sliderVolume.Value = (double)Cfg.Default.RadioVolume;
				this.txtNickname.Text = Cfg.Default.IrcNickname
					= Environment.UserName.Trim().Replace(" ", String.Empty);
			}
		}

		private void btnSave_Click(object sender, RoutedEventArgs e)
		{
			if (this.IsRestartRequired)
			{
				MessageBox.Show(
					Res.MsgRestartRequired,
					"Application Restart Required",
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
			}

			if (Cfg.Default.IrcNickname.Trim().Length == 0) Cfg.Default.IrcNickname = "SharpLeech_User";
			else Cfg.Default.IrcNickname = Cfg.Default.IrcNickname.Trim().Replace(" ", "_");

			Cfg.Default.Save();
			this.DialogResult = true;
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			Cfg.Default.Reload();
			this.DialogResult = false;
		}

		private void cbServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.IsRestartRequired = true;
		}

		private void checkWin7_Checked(object sender, RoutedEventArgs e)
		{
			this.IsRestartRequired = true;
		}

		private void checkAero_Checked(object sender, RoutedEventArgs e)
		{
			this.IsRestartRequired = true;
		}

		private void txtNickname_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!this.IsNicknameChanged) this.IsNicknameChanged = true;
		}
	}
}
