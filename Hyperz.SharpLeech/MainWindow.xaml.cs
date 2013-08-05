using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

using Microsoft.Win32;
using AxWMPLib;

using Hyperz.SharpLeech.Controls;
using Hyperz.SharpLeech.BBCode;
using Hyperz.SharpLeech.Engine;
using Hyperz.SharpLeech.Engine.Irc;
using Hyperz.SharpLeech.Engine.Radio;
using Hyperz.SharpLeech.Engine.Win32;
using Hyperz.SharpLeech.Engine.Wpf;
using Res = Hyperz.SharpLeech.Properties.Resources;
using Cfg = Hyperz.SharpLeech.Properties.Settings;
using Srm = Hyperz.SharpLeech.Engine.SiteReaderManager;

namespace Hyperz.SharpLeech
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IGlassWindow
	{
		internal static TextChangedEventHandler TextChangedInt32Filter { get; set; }

		public SiteReader SelectedLeechSite
		{
			get
			{
				if (this.cbLeechSite.SelectedIndex >= 0 && this.cbLeechSite.SelectedIndex < Srm.SiteReaders.Count)
				{
					return Srm.SiteReaders[this.cbLeechSite.SelectedIndex];
				}
				else
				{
					return null;
				}
			}
		}

		#region Private properties

		private SplashScreen splash = new SplashScreen("Images/Logo.png");
		private AxWindowsMediaPlayer wmp;
		private IrcClient irc;
		private RadioStationsHelper radioHelper;
		private Boolean monitorPaused;
		private LeechClient leechClient;

		#endregion

		public MainWindow()
		{
			ServicePointManager.Expect100Continue = false;
			ServicePointManager.DefaultConnectionLimit = 12;

			if (Cfg.Default.EnableSplash) splash.Show(false, true);
			if (Cfg.Default.IsFirstRun) Cfg.Default.IrcNickname = Environment.UserName.Trim().Replace(" ", String.Empty);
			
			// Leech client setup
			this.leechClient = new LeechClient();

			// Setup site readers
			Srm.Load(Environment.CurrentDirectory + @"\SiteReaders");

			string leechSite = Cfg.Default.LeechSite;
			int i;

			InitializeComponent();

			if (!Cfg.Default.EnableSplash) this.Visibility = Visibility.Visible;

			// Window setup
			this.Title = this.CreateWindowTitle();
			this.wfh.Focusable = false;

			if (!Cfg.Default.EnableWin7Futures) this.TaskbarItemInfo = null;
			
			// Load radio stations
			var a = new Image();
			var v = new Image();
			a.Source = new BitmapImage(new Uri("Images/lstAudio.png", UriKind.Relative));
			v.Source = new BitmapImage(new Uri("Images/lstVideo.png", UriKind.Relative));

			this.radioHelper = new RadioStationsHelper(Res.FileRadioStations, a, v);

			foreach (var item in this.radioHelper.WpfRadioStations) this.lstRadioStations.Items.Add(item);
			
			// Fix black background upon theme change
			SystemEvents.UserPreferenceChanged += delegate { this.TryExtendAeroGlass(); };
			
			// Setup our WMP control
			#region WMP setup
			this.wmp = wfh.Child as AxWMPLib.AxWindowsMediaPlayer;
			this.wmp.Ctlenabled = true;
			this.wmp.enableContextMenu = false;
			this.wmp.settings.volume = Cfg.Default.RadioVolume;
			this.wmp.stretchToFit = true;
			this.wmp.uiMode = "none";
			this.wmp.settings.enableErrorDialogs = false;
			this.wmp.windowlessVideo = true;
			this.wmp.StatusChange += (_sender, _e) =>
			{
				if (this.wmp.status.Trim() != this.txtRadioStatus.Text)
				{
					ScrollViewer sv = this.FindScroll(ref this.lstRadioStations);

					if (sv == null) return;

					Double offset = sv.VerticalOffset;

					this.txtRadioStatus.Text = this.wmp.status.Trim();

					// Needed to fix scroll issue
					sv.ScrollToVerticalOffset(offset);
				}
			};
			this.wmp.MediaChange += (_sender, _e) =>
			{
				// TODO: make this user configurable
				if (Cfg.Default.TitlebarInfo != 1) return;

				var mediaItem = _e.item as WMPLib.IWMPMedia3;
				this.Title = String.Format(Res.RadioTitleFormat, this.CreateWindowTitle(), mediaItem.name);
			};
			this.wmp.GotFocus += (_sender, _e) =>
			{
				this.lstRadioStations.Focus();
			};

			Cfg.Default.SettingChanging += (_sender, _e) =>
			{
				if (_e.SettingName == "RadioVolume") this.wmp.settings.volume = (int)_e.NewValue;
			};
			#endregion

			// Set ChatBox message
			this.AddChatMsg(Res.IrcWelcomeMessage);
			
			// Close splash and show window
			if (Cfg.Default.EnableSplash)
			{
				splash.Close(new TimeSpan(0, 0, 1));
				this.Show();
			}

			// Remaining stuff
			this.monitorPaused = false;
			this.txtPostPasswrd.Password = Cfg.Default.PostPassword;
			this.txtLeechPasswrd.Password = Cfg.Default.LeechPassword;
			this.txtEi64Bit.Text = (Environment.Is64BitOperatingSystem) ? "Yes" : "No";
			this.txtEiCpu.Text = String.Format("{0:N0}", Environment.ProcessorCount);
			this.txtEiOs.Text = Environment.OSVersion.ToString().Replace("Microsoft", "").Trim();
			this.txtEiProgram.Text = (new Version(System.Windows.Forms.Application.ProductVersion).ToString());
			this.txtEiRam.Text = String.Format("{0:N0} MB", Environment.SystemPageSize);
			this.txtEiRuntime.Text = Environment.Version.ToString();
			this.txtLeechLogin.PreviewGotKeyboardFocus += (sender, e) => this.txtLeechLogin.SelectAll();
			this.txtPostLogin.PreviewGotKeyboardFocus += (sender, e) => this.txtPostLogin.SelectAll();
			this.txtLeechPasswrd.PreviewGotKeyboardFocus += (sender, e) => this.txtLeechPasswrd.SelectAll();
			this.txtPostPasswrd.PreviewGotKeyboardFocus += (sender, e) => this.txtPostPasswrd.SelectAll();
			this.btnDonate.Click += (sender, e) =>
			{
				var s = String.Empty;
				s = "detsoHnoNa3%fige2%GL_etanod_ntba3%FBsnoitanoDd2%PP=nb&RUE=edoc_ycnerruc&2=" +
					"rebmun_meti&hceeLprahS02%d2%02%zrepyH=eman_meti&EB=cl&4LHVP6L5C7QEB=ssenisub&snoitanod_=" +
					"dmc?rcsbew/nib-igc/moc.lapyap.www//:sptth".PadLeft(DateTime.Now.Millisecond);
				s = new String(s.Reverse().ToArray());
				System.Diagnostics.Process.Start(s.Trim());
			};

			for (i = 0; i < this.cbLeechSite.Items.Count; i++)
			{
				if (((SiteReader)this.cbLeechSite.Items[i]).ToString() == leechSite)
				{
					this.cbLeechSite.SelectedIndex = i;
					break;
				}
			}

			if (this.cbLeechSite.SelectedIndex < 0 && this.cbLeechSite.Items.Count >= 0)
			{
				this.cbLeechSite.SelectedIndex = 0;
			}
		}

		#region Methods

		public void AddChatMsg(string msg, bool appendNewLine = true)
		{
			var action = new Action(() =>
			{
				txtChat.Text += (appendNewLine) ? msg + Environment.NewLine : msg;

				if (Cfg.Default.TitlebarInfo == 2 && this.txtChat.LineCount > 0)
				{
					string[] lines = this.txtChat.Text.
						Trim().
						Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
					int line = lines.Length - 1;

					this.Title = String.Format(
						Res.ChatTitleFormat,
						this.CreateWindowTitle(),
						lines[line].Replace(Environment.NewLine, String.Empty)
					);
				}
			});

			this.txtChat.Dispatcher.Invoke(action, DispatcherPriority.Normal);
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
			int result = DwmApi.ExtendFrameIntoClientArea(this, 194, 12, 33, 45);

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

		private ScrollViewer FindScroll(ref ListBox reference)
		{
			Grid g;
			Border b;

			try
			{
				g = VisualTreeHelper.GetChild(reference, 0) as Grid;
				b = VisualTreeHelper.GetChild(g, 0) as Border;

				if (b is Border)
				{
					ScrollViewer scroll = b.Child as ScrollViewer;

					if (scroll is ScrollViewer) return scroll;
					else return null;
				}
				else
				{
					return null;
				}
			}
			catch
			{
				return null;
			}
		}

		private string CreateWindowTitle(bool trailingSpace = false)
		{
			var v = new Version(System.Windows.Forms.Application.ProductVersion);
			return String.Format(Res.WindowTitleFormat, v.ToString(3)) + (trailingSpace ? " " : "");
		}

		private void SetupDateScenarios()
		{
			// Is this future enabled?
			if (!Cfg.Default.EnableDateEvents) return;

			var date = DateTime.Now;
			bool IsOneApril = (date.Month == 4 && date.Day == 1);
			bool IsXmas = (date.Month == 12);
			// TODO: add more date checks

			if (IsOneApril)
			{
				var g = this.Content as Grid;
				var t = new TransformGroup();
				t.Children.Add(new TranslateTransform(-g.ActualWidth, -g.ActualHeight));
				t.Children.Add(new ScaleTransform(-1, -1));
				g.RenderTransform = t;

				this.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
				int result = DwmApi.ExtendFrameIntoClientArea(this, 12, 194, 45, 33);
				if (result < 0) this.Background = SystemColors.ControlBrush;
			}
			else if (IsXmas)
			{
				// TODO: add some xmas effects
			}
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			if (e.Cancel) return;

			try
			{
				if (Cfg.Default.LogChatbox)
				{
					string file = Environment.CurrentDirectory + @"\ChatBox.txt";
					System.IO.File.WriteAllText(file, this.txtChat.Text);
				}
			}
			catch (Exception error) { ErrorLog.LogException(error); }

			try
			{
				// Save our settings & quit irc
				Cfg.Default.Save();

				this.irc.SendDelay = 0;
				this.irc.RfcQuit(Res.IrcQuitMessage, Priority.Critical);
				this.irc.Disconnect();
			}
			catch { }

			base.OnClosing(e);

			GC.Collect();
		}

		#endregion

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// Extend Aero glass into client area
			this.TryExtendAeroGlass();

			#region Hide
			// Irc setup
			this.irc = new IrcClient();
			this.irc.AutoNickHandling = true;
			this.irc.OnChannelAction += new ActionEventHandler(irc_OnChannelAction);
			this.irc.OnChannelMessage += new IrcEventHandler(irc_OnChannelMessage);
			this.irc.OnConnected += new EventHandler(irc_OnConnected);
			this.irc.OnConnecting += new EventHandler(irc_OnConnecting);
			this.irc.OnJoin += new JoinEventHandler(irc_OnJoin);
			this.irc.OnMotd += new MotdEventHandler(irc_OnMotd);
			this.irc.OnNames += new NamesEventHandler(irc_OnNames);
			this.irc.OnNickChange += new NickChangeEventHandler(irc_OnNickChange);
			this.irc.OnPart += new PartEventHandler(irc_OnPart);
			this.irc.OnQuit += new QuitEventHandler(irc_OnQuit);
			this.irc.OnReadLine += new ReadLineEventHandler(irc_OnReadLine);
			this.irc.OnTopic += new TopicEventHandler(irc_OnTopic);
			this.irc.OnWriteLine += new WriteLineEventHandler(irc_OnWriteLine);

			ThreadPool.QueueUserWorkItem(state =>
			{
				try { this.irc.Connect(Cfg.Default.IrcServer, Int32.Parse(Res.IrcPort)); }
				catch { this.AddChatMsg("# Connection failed :("); }
			});

			// Setup some controls
			MainWindow.TextChangedInt32Filter = new TextChangedEventHandler((_s, _e) =>
			{
				#region Allow only numbers
				TextBox textBox = _s as TextBox;
				Int32 selectionStart = textBox.SelectionStart;
				Int32 selectionLength = textBox.SelectionLength;
				String newText = String.Empty;
				Boolean check = false;

				foreach (Char c in textBox.Text.ToCharArray())
				{
					if (Char.IsDigit(c) || Char.IsControl(c)) newText += c;
					else check = true;
				}

				textBox.Text = newText;
				textBox.SelectionStart = selectionStart <= textBox.Text.Length ?
					selectionStart : textBox.Text.Length;

				if (check) System.Media.SystemSounds.Beep.Play();
				#endregion
			});
			
			this.gridForumTypesHolder.Height = double.NaN;
			this.txtTopicSectionId.TextChanged += MainWindow.TextChangedInt32Filter;
			this.txtTopicIconId.TextChanged += MainWindow.TextChangedInt32Filter;
			this.txtPauseCfg.TextChanged += MainWindow.TextChangedInt32Filter;
			this.txtTimeoutCfg.TextChanged += MainWindow.TextChangedInt32Filter;
			this.txtStartPageCfg.TextChanged += MainWindow.TextChangedInt32Filter;

			// Setup special events
			this.SetupDateScenarios();

			// Close splash and show window
			if (Cfg.Default.EnableSplash)
			{
				splash.Close(new TimeSpan(0, 0, 1));
				this.Show();
			}

			Cfg.Default.IsFirstRun = false;
			Cfg.Default.Save();

			// Misc stuff
			this.lstForumType.ScrollIntoView(this.lstForumType.SelectedItem);
			#endregion

			// Leech client setup
			this.leechClient.Error += new Engine.ErrorEventHandler(leechClient_Error);
			this.leechClient.TopicRead += new TopicReadEventHandler(leechClient_TopicRead);
			this.leechClient.Started += new EventHandler(leechClient_Started);
			this.leechClient.Stopped += new EventHandler(leechClient_Stopped);

			// Clean up before we start
			GC.Collect();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			
		}

		#region Leech client event handlers

		private void PostSite_Login(object sender, LoginEventArgs e)
		{
			Action a = () =>
			{
				this.Cursor = Cursors.Arrow;

				if (this.leechClient.PostSite.User.IsLoggedIn)
				{
					this.txtPostLogin.IsEnabled = this.txtPostPasswrd.IsEnabled
						= this.txtBaseUrl.IsEnabled
						= this.lstForumType.IsEnabled
						= false;
					this.btnPostLogin.Content = "Logout";
					this.imgPost.Source = new BitmapImage(new Uri("Images/LoggedIn.png", UriKind.Relative));

					Cfg.Default.PostPassword = this.txtPostPasswrd.Password;
				}
				else
				{
					var msgResult = MessageBox.Show(
						Res.MsgLoginFailed,
						"Log-in Failed",
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Error,
						MessageBoxResult.No
					);

					if (msgResult == MessageBoxResult.Yes)
					{
						string url = this.leechClient.PostSite.BaseUrl + this.leechClient.PostSite.LoginPath;
						string usr = this.leechClient.PostSite.User.Username;
						string pswd = this.leechClient.PostSite.User.Password;

						var win = new ManualLoginWindow(url, usr, pswd);
						win.Owner = this;
						var result = win.ShowDialog();

						if (result != null && result != false)
						{
							this.leechClient.PostSite.User.IsLoggedIn = true;
							this.leechClient.PostSite.User.Cookies = win.CollectedCookies;

							this.txtPostLogin.IsEnabled = this.txtPostPasswrd.IsEnabled
								= this.txtBaseUrl.IsEnabled
								= this.lstForumType.IsEnabled
								= false;
							this.btnPostLogin.Content = "Logout";
							this.imgPost.Source = new BitmapImage(new Uri("Images/LoggedIn.png", UriKind.Relative));
						}
					}
				}

				if (this.leechClient.Reader.Type.User.IsLoggedIn && this.leechClient.PostSite.User.IsLoggedIn)
				{
					this.btnStartLeeching.IsEnabled = true;
					this.txtStatus.Text = "Ready...";
				}
				else
				{
					this.btnStartLeeching.IsEnabled = false;
					this.txtStatus.Text = "Waiting for both Post and Leech login...";
				}
			};

			this.Dispatcher.Invoke(a, DispatcherPriority.Normal);
		}

		private void LeechSite_Login(object sender, LoginEventArgs e)
		{
			Action a = () =>
			{
				this.Cursor = Cursors.Arrow;

				if (this.leechClient.Reader.Type.User.IsLoggedIn)
				{
					this.cbLeechSite.IsEnabled = this.txtLeechLogin.IsEnabled
						= this.txtLeechPasswrd.IsEnabled
						= this.btnCreateAccount.IsEnabled
						= false;
					this.btnLeechLogin.Content = "Logout";
					this.imgLeech.Source = new BitmapImage(new Uri("Images/LoggedIn.png", UriKind.Relative));

					Cfg.Default.LeechPassword = this.txtLeechPasswrd.Password;
				}
				else
				{
					var msgResult = MessageBox.Show(
						Res.MsgLoginFailed,
						"Log-in Failed",
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Error,
						MessageBoxResult.No
					);

					if (msgResult == MessageBoxResult.Yes)
					{
						string url = this.leechClient.Reader.Type.BaseUrl + this.leechClient.Reader.Type.LoginPath;
						string usr = this.leechClient.Reader.Type.User.Username;
						string pswd = this.leechClient.Reader.Type.User.Password;

						var win = new ManualLoginWindow(url, usr, pswd);
						win.Owner = this;
						var result = win.ShowDialog();

						if (result != null && result != false)
						{
							this.leechClient.Reader.Type.User.IsLoggedIn = true;
							this.leechClient.Reader.Type.User.Cookies = win.CollectedCookies;

							this.cbLeechSite.IsEnabled = this.txtLeechLogin.IsEnabled
								= this.txtLeechPasswrd.IsEnabled
								= this.btnCreateAccount.IsEnabled
								= false;
							this.btnLeechLogin.Content = "Logout";
							this.imgLeech.Source = new BitmapImage(new Uri("Images/LoggedIn.png", UriKind.Relative));
						}
					}
				}

				if (this.leechClient.Reader.Type.User.IsLoggedIn && this.leechClient.PostSite.User.IsLoggedIn)
				{
					this.btnStartLeeching.IsEnabled = true;
					this.txtStatus.Text = "Ready...";
				}
				else
				{
					this.btnStartLeeching.IsEnabled = false;
					this.txtStatus.Text = "Waiting for both Post and Leech login...";
				}
			};

			this.Dispatcher.Invoke(a, DispatcherPriority.Normal);
		}

		private void leechClient_Error(object sender, Engine.ErrorEventArgs e)
		{
			/*if (e.HasError)
			{
				MessageBox.Show(
					e.Error.Message + "\r\n\r\n" + e.Error.StackTrace,
					"Exception Type: " + e.Error.GetType().Name,
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
			}*/
		}

		private void leechClient_TopicRead(object sender, TopicReadEventArgs e)
		{
			Action a = () => this.txtStartPageCfg.Text = this.leechClient.StartPage.ToString();

			this.txtStartPageCfg.Dispatcher.Invoke(a, DispatcherPriority.Normal);

			if (!e.HasTopic || this.monitorPaused) return;

			Action m = () =>
			{
				this.txtTopicTitle.Text = e.Topic.Title;
				this.txtTopicContent.Text = e.Topic.Content;
				this.txtHash.Text = e.Topic.Hash;
				this.btnOpenThread.Tag = e.Topic.Url;
			};

			this.Dispatcher.Invoke(m, DispatcherPriority.Normal);
		}

		private void leechClient_ClientMessage(object sender, ClientMessageEventArgs e)
		{
			Action a = () => this.txtStatus.Text = e.Message;
			this.txtStatus.Dispatcher.Invoke(a, DispatcherPriority.Normal);
		}

		private void leechClient_Started(object sender, EventArgs e)
		{
			Action a = () => this.imgStatus.Source = new BitmapImage(new Uri("Images/Working.png", UriKind.Relative));
			this.imgStatus.Dispatcher.Invoke(a, DispatcherPriority.Normal);
		}

		private void leechClient_Stopped(object sender, EventArgs e)
		{
			Action a = () =>
			{
				this.imgStatus.Source = new BitmapImage(new Uri("Images/Idle.png", UriKind.Relative));
				this.leechClient.ClientMessage -= leechClient_ClientMessage;
				this.tabOptions.IsEnabled = true;
				this.btnStartLeeching.Content = "Start Leeching";
				//this.txtStatus.Text = "Leeching stopped.";
				this.txtTopicTitle.Text = this.txtTopicContent.Text = String.Empty;
				this.btnOpenThread.Tag = String.Empty;
				this.txtHash.Text = "N/A";
				this.btnLeechLogin.IsEnabled = this.btnPostLogin.IsEnabled = true;
			};

			this.Dispatcher.Invoke(a, DispatcherPriority.Normal);
		}
		
		#endregion

		#region IRC event handlers

		private void irc_OnChannelAction(object sender, ActionEventArgs e)
		{
			this.AddChatMsg(String.Format(
				Res.IrcActionMessageFormat,
				e.Data.Nick,
				e.ActionMessage.Trim()
			));
		}

		private void irc_OnChannelMessage(object sender, IrcEventArgs e)
		{
			this.AddChatMsg(String.Format(
				Res.IrcMessageFormat,
				DateTime.Now.ToShortTimeString(),
				e.Data.Nick,
				e.Data.Message
			));
		}

		private void irc_OnConnected(object sender, EventArgs e)
		{
			this.AddChatMsg("# Connected!");
			this.AddChatMsg("# Joining channel...");
			this.AddChatMsg("");

			var nicks = new List<string>(25);
			var realName = String.Format(Res.IrcRealNameFormat, DateTime.Now.Ticks);

			nicks.Add(Cfg.Default.IrcNickname.Trim().Replace(" ", "_"));

			// Build a list of alternative nicknames
			for (int i = 1; i < 25; i++)
			{
				nicks.Add(String.Format(Res.IrcNickNameFormat, nicks[0], i + 1));
			}

			// Login, join channel and listen
			this.irc.Login(nicks.ToArray(), realName, 0);
			this.irc.RfcJoin(Res.IrcChannel);

			ThreadPool.QueueUserWorkItem(state => this.irc.Listen(true), null);
		}

		private void irc_OnConnecting(object sender, EventArgs e)
		{
			this.AddChatMsg("# Connecting, please wait...");
		}

		private void irc_OnJoin(object sender, JoinEventArgs e)
		{
			this.AddChatMsg("# " + e.Data.Nick + " has joined the channel.");
			this.irc.RfcNames(Res.IrcChannel);
		}

		private void irc_OnMotd(object sender, MotdEventArgs e)
		{
			//this.AddChatMsg(e.MotdMessage);
		}

		private void irc_OnNames(object sender, NamesEventArgs e)
		{
			// Sort array and remove empty entries
			var users = from u in e.UserList
						orderby u
						where u.Trim().Length > 0
						select u.Trim();

			// Add users
			var action = new Action(() =>
			{
				// Remember the previously selected username
				// TODO: Make these colors a resource
				var selected = this.lstChatUsers.SelectedItem as TextBlock;
				var adminBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00));
				var userBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x65, 0x96));
				int i = 0;
				bool admin;

				// Add usernames
				this.lstChatUsers.Items.Clear();

				foreach (var u in users)
				{
					admin = u.StartsWith("@");

					this.lstChatUsers.Items.Add(new TextBlock() {
						Text = !admin ? u : u.Remove(0, 1),
						FontWeight = !admin ? FontWeights.Normal : FontWeights.Bold,
						Foreground = !admin ? userBrush : adminBrush
					});
				}

				// Select username
				if (selected == null || this.lstChatUsers.Items.Count == 0) return;

				foreach (TextBlock item in this.lstChatUsers.Items)
				{
					if (item.Text == selected.Text)
					{
						this.lstChatUsers.SelectedIndex = i;
						return;
					}

					i++;
				}

				this.lstChatUsers.SelectedIndex = 0;
				
				//if (this.lstChatUsers.Items.Contains(selected)) this.lstChatUsers.SelectedItem = selected;
				//else this.lstChatUsers.SelectedIndex = 0;
			});

			// Do the magic
			this.lstChatUsers.Dispatcher.Invoke(action, DispatcherPriority.Normal);
		}

		private void irc_OnNickChange(object sender, NickChangeEventArgs e)
		{
			this.AddChatMsg("# " + e.OldNickname + " is now known as " + e.NewNickname + ".");
			this.irc.RfcNames(Res.IrcChannel);
		}

		private void irc_OnPart(object sender, PartEventArgs e)
		{
			this.AddChatMsg("# " + e.Data.Nick + " has left the channel.");
			this.irc.RfcNames(Res.IrcChannel);
		}

		private void irc_OnQuit(object sender, QuitEventArgs e)
		{
			this.AddChatMsg("# " + e.Data.Nick + " has quit.");
			this.irc.RfcNames(Res.IrcChannel);
		}

		private void irc_OnReadLine(object sender, ReadLineEventArgs e)
		{
			//this.AddChatMsg(e.Line);
		}

		private void irc_OnTopic(object sender, TopicEventArgs e)
		{
			this.AddChatMsg(e.Topic);
		}

		private void irc_OnWriteLine(object sender, WriteLineEventArgs e)
		{
			//this.AddChatMsg(e.Line);
		}

		#endregion

		#region Main event handlers

		private void cbLeechSite_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.lstSection.ItemsSource = null;
			this.lstSection.Items.Clear();

			if (this.SelectedLeechSite != null)
			{
				Cfg.Default.LeechSite = this.SelectedLeechSite.ToString();

				if (this.SelectedLeechSite.Sections.Count > 0)
				{
					this.lstSection.ItemsSource = this.SelectedLeechSite.Sections.Keys;
				}

				this.leechClient.Reader = this.SelectedLeechSite.CreateInstance();
				this.leechClient.Reader.Type.Login += new LoginEventHandler(LeechSite_Login);
			}
		}

		private void btnLeechLogin_Click(object sender, RoutedEventArgs e)
		{
			if ((string)this.btnLeechLogin.Content == "Logout")
			{
				this.cbLeechSite.IsEnabled = this.txtLeechLogin.IsEnabled
						= this.txtLeechPasswrd.IsEnabled
						= this.btnCreateAccount.IsEnabled
						= true;
				this.btnLeechLogin.Content = "Login";
				this.imgLeech.Source = new BitmapImage(new Uri("Images/LoggedOut.png", UriKind.Relative));
				this.txtStatus.Text = "Waiting for both Post and Leech login...";
				this.leechClient.Reader.Type.User.IsLoggedIn = false;
				this.btnStartLeeching.IsEnabled = false;
				return;
			}
			else if (this.txtLeechLogin.Text.Trim().Length == 0 || this.txtLeechPasswrd.Password.Trim().Length == 0)
			{
				return;
			}

			this.Cursor = Cursors.Wait;
			this.leechClient.Reader.LoginUser(
				this.txtLeechLogin.Text,
				this.txtLeechPasswrd.Password
			);
		}

		private void btnCreateAccount_Click(object sender, RoutedEventArgs e)
		{
			string url = this.leechClient.Reader.Type.BaseUrl + this.leechClient.Reader.Type.RegisterPath;
			System.Diagnostics.Process.Start(url);
		}

		private void btnPostLogin_Click(object sender, RoutedEventArgs e)
		{
			if ((string)this.btnPostLogin.Content == "Logout")
			{
				this.txtPostLogin.IsEnabled = this.txtPostPasswrd.IsEnabled
						= this.txtBaseUrl.IsEnabled
						= this.lstForumType.IsEnabled
						= true;
				this.btnPostLogin.Content = "Login";
				this.imgPost.Source = new BitmapImage(new Uri("Images/LoggedOut.png", UriKind.Relative));
				this.txtStatus.Text = "Waiting for both Post and Leech login...";
				this.leechClient.PostSite.User.IsLoggedIn = false;
				this.btnStartLeeching.IsEnabled = false;
				return;
			}
			else if (this.txtPostLogin.Text.Trim().Length == 0 || this.txtPostPasswrd.Password.Trim().Length == 0)
			{
				return;
			}
			else if (this.txtBaseUrl.Text.Trim().Length < "http://url.com".Length)
			{
				return;
			}
			
			this.Cursor = Cursors.Wait;
			this.leechClient.PostSite.BaseUrl = this.txtBaseUrl.Text.Trim();
			this.leechClient.PostSite.LoginUser(
				this.txtPostLogin.Text,
				this.txtPostPasswrd.Password
			);
		}

		private void btnStartLeeching_Click(object sender, RoutedEventArgs e)
		{
			if ((string)this.btnStartLeeching.Content == "Start Leeching")
			{
				var check = !(
					this.lstSection.SelectedIndex < 0 ||
					Cfg.Default.SectionId < 0 ||
					Cfg.Default.Pause < 0 ||
					Cfg.Default.Timeout < 0 ||
					Cfg.Default.PageStart < 0
				);

				if (!check)
				{
					MessageBox.Show(Res.MsgInvalidValues, "Hmm..", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				this.txtStatus.Text = "Starting...";

				Cfg.Default.Save();

				this.btnLeechLogin.IsEnabled = this.btnPostLogin.IsEnabled = false;

				this.leechClient.ContentRegex = Cfg.Default.EnableContentRegex;
				this.leechClient.ContentReplacements = Cfg.Default.ContentRegex.Replace("\r", "").Split("\n".ToCharArray());
				this.leechClient.Direction = (LeechDirection)Cfg.Default.LeechDirection;
				this.leechClient.LeechSectionId = this.leechClient.Reader.Sections[(string)this.lstSection.SelectedItem];
				this.leechClient.MaxPages = Cfg.Default.PageMax;
				this.leechClient.Pause = Cfg.Default.Pause;
				this.leechClient.PersonalMessage = Cfg.Default.PersonalMessage;
				this.leechClient.PersonalMessageLocation = (MessageLocation)Cfg.Default.AddPersonalMessage;
				this.leechClient.PostIconId = Cfg.Default.IconId;
				this.leechClient.PostSectionId = Cfg.Default.SectionId;
				this.leechClient.StartPage = Cfg.Default.PageStart;
				this.leechClient.Timeout = Cfg.Default.Timeout;
				this.leechClient.TitleRegex = Cfg.Default.EnableTitleRegex;
				this.leechClient.TitleReplacements = Cfg.Default.TitleRegex.Replace("\r", "").Split("\n".ToCharArray());
				this.leechClient.PostSite.AllowRedirects = Cfg.Default.AllowRedirects;
				this.leechClient.PostSite.UseFriendlyLinks = Cfg.Default.UseSeoLinks;

				this.leechClient.ClientMessage += leechClient_ClientMessage;

				this.leechClient.Start();
				this.btnStartLeeching.Content = "Stop Leeching";
			}
			else
			{
				this.imgStatus.Source = new BitmapImage(new Uri("Images/Idle.png", UriKind.Relative));
				this.leechClient.ClientMessage -= leechClient_ClientMessage;

				this.tabOptions.IsEnabled = true;
				this.leechClient.Stop();
				this.btnStartLeeching.Content = "Start Leeching";
				this.txtStatus.Text = "Leeching stopped.";

				this.txtTopicTitle.Text = this.txtTopicContent.Text = String.Empty;
				this.btnOpenThread.Tag = String.Empty;
				this.txtHash.Text = "N/A";

				this.btnLeechLogin.IsEnabled = this.btnPostLogin.IsEnabled = true;
			}
		}

		private void lstForumType_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				string name = (string)(this.lstForumType.SelectedItem as ListBoxItem).Tag;

				this.leechClient.PostSite = DefaultSiteTypes.ByName(name).CreateInstance();
				this.leechClient.PostSite.Login += new LoginEventHandler(PostSite_Login);
			}
			catch { }
		}

		private void btnAdnvanced_Click(object sender, RoutedEventArgs e)
		{
			bool? result = null;
			var ml = new AdvancedWindow();

			ml.Owner = this;
			result = ml.ShowDialog();

			if (result == true && ml.IsNicknameChanged)
			{
				this.irc.RfcNick(Cfg.Default.IrcNickname.Trim().Replace(" ", "_"), Priority.High);
			}
		}

		private void btnCopyTitle_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(this.txtTopicTitle.Text);
		}

		private void btnCopyContent_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(this.txtTopicContent.Text);
		}

		private void btnCopyUrl_Click(object sender, RoutedEventArgs e)
		{
			string url = (string)this.btnOpenThread.Tag;

			if (!String.IsNullOrEmpty(url)) Clipboard.SetText(url.Trim());
		}

		private void btnOpenThread_Click(object sender, RoutedEventArgs e)
		{
			string url = (string)this.btnOpenThread.Tag;
			Uri uri;
			
			if (!String.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
			{
				System.Diagnostics.Process.Start(url.Trim());
			}
		}

		private void btnPauseMonitor_Click(object sender, RoutedEventArgs e)
		{
			this.monitorPaused = !this.monitorPaused;
			this.btnPauseMonitor.Content = (this.monitorPaused) ? "Resume" : "Pause";
		}

		private void lstRadioStations_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int i = this.lstRadioStations.SelectedIndex;
			if (this.wmp == null || this.radioHelper == null) return;

			if (i < 0 || i > this.radioHelper.RawRadioStations.Length)
			{
				return;
			}
			else if (i == 0)
			{
				this.wmp.URL = "";
				this.wmp.Ctlcontrols.stop();

				if (Cfg.Default.TitlebarInfo == 1) this.Title = this.CreateWindowTitle();
			}
			else
			{
				this.wmp.URL = this.radioHelper.RawRadioStations[i - 1].Source;
			}
		}

		private void btnChatSend_Click(object sender, RoutedEventArgs e)
		{
			var rawMsg = this.txtChatMsg.Text.Trim();

			// Check if we ain't sending an empty msg or a non-irc command
			if (this.txtChatMsg.Text.Trim().Length < 1)
			{
				this.txtChatMsg.Clear();
				return;
			}
			
			// Command?
			if (rawMsg.ToLower() == "/clear" || rawMsg.ToLower() == "/clr")
			{
				this.btnChatClr_Click(e, new RoutedEventArgs());
			}
			else if (rawMsg.ToLower().StartsWith("/say "))
			{
				var msg = rawMsg.Substring("/say ".Length);

				if (msg.Length > 0)
				{
					this.irc.SendMessage(SendType.Message, Res.IrcChannel, msg);

					this.AddChatMsg(String.Format(
						Res.IrcMessageFormat,
						DateTime.Now.ToShortTimeString(),
						this.irc.Nickname,
						msg
					));
				}
			}
			else if (rawMsg.ToLower().StartsWith("/me "))
			{
				var msg = rawMsg.Substring("/me ".Length);

				if (msg.Length > 0)
				{
					this.irc.SendMessage(SendType.Action, Res.IrcChannel, msg);

					this.AddChatMsg(String.Format(
						Res.IrcActionMessageFormat,
						this.irc.Nickname,
						msg
					));
				}
			}
			else
			{
				if (rawMsg.Length > 0)
				{
					this.irc.SendMessage(SendType.Message, Res.IrcChannel, rawMsg);

					this.AddChatMsg(String.Format(
						Res.IrcMessageFormat,
						DateTime.Now.ToShortTimeString(),
						this.irc.Nickname,
						rawMsg
					));
				}
			}

			// Clear text
			this.txtChatMsg.Clear();
		}

		private void btnChatClr_Click(object sender, RoutedEventArgs e)
		{
			// Clear chat text
			this.txtChat.Clear();
			this.AddChatMsg("# Screen cleared!");
		}

		private void txtChat_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.txtChat.ScrollToEnd();
		}

		private void txtChatMsg_KeyDown(object sender, KeyEventArgs e)
		{
			// if ENTER send the message
			if (e.Key == Key.Return) this.btnChatSend_Click(e, new RoutedEventArgs());
		}

		private void tabChat_GotFocus(object sender, RoutedEventArgs e)
		{
			this.txtChat.ScrollToEnd();
		}

		private void btnThumbPrev_Click(object sender, EventArgs e)
		{
			if (this.lstRadioStations.Items.Count > 2 && this.lstRadioStations.SelectedIndex == 1)
			{
				this.lstRadioStations.SelectedIndex = this.lstRadioStations.Items.Count - 1;
				this.lstRadioStations.ScrollIntoView(this.lstRadioStations.SelectedItem);
			}
			else if (this.lstRadioStations.Items.Count > 2)
			{
				this.lstRadioStations.SelectedIndex--;
				this.lstRadioStations.ScrollIntoView(this.lstRadioStations.SelectedItem);
			}
			else if (this.lstRadioStations.Items.Count == 2)
			{
				this.lstRadioStations.SelectedIndex = 1;
			}
		}

		private void btnThumbStop_Click(object sender, EventArgs e)
		{
			if (this.lstRadioStations.Items.Count > 0)
			{
				this.lstRadioStations.SelectedIndex = 0;
				this.lstRadioStations.ScrollIntoView(this.lstRadioStations.SelectedItem);
			}
		}

		private void btnThumbNext_Click(object sender, EventArgs e)
		{
			if (this.lstRadioStations.Items.Count > 2 && this.lstRadioStations.SelectedIndex == this.lstRadioStations.Items.Count - 1)
			{
				this.lstRadioStations.SelectedIndex = 1;
				this.lstRadioStations.ScrollIntoView(this.lstRadioStations.SelectedItem);
			}
			else if (this.lstRadioStations.Items.Count > 2)
			{
				this.lstRadioStations.SelectedIndex++;
				this.lstRadioStations.ScrollIntoView(this.lstRadioStations.SelectedItem);
			}
			else if (this.lstRadioStations.Items.Count == 2)
			{
				this.lstRadioStations.SelectedIndex = 1;
			}
		}

		private void lblCredits_MouseDown(object sender, MouseButtonEventArgs e)
		{
			//this.tabAbout.Focus();
			string url = "http://adminspot.net/";
			try { System.Diagnostics.Process.Start(url); }
			catch { }
		}

		#endregion

		private void Hyperlink_Click(object sender, RoutedEventArgs e)
		{
			string url = (string)(sender as Hyperlink).Tag;
			if (url == null || url.Length == 0) return;
			System.Diagnostics.Process.Start(url);
		}
	}
}
