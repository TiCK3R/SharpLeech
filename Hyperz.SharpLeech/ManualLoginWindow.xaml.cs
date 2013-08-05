using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Hyperz.SharpLeech.Engine;
using Hyperz.SharpLeech.Engine.Net;
using wf = System.Windows.Forms;

namespace Hyperz.SharpLeech
{
	/// <summary>
	/// Interaction logic for ManualLoginWindow.xaml
	/// </summary>
	public partial class ManualLoginWindow : Window
	{
		public CookieCollection CollectedCookies { get; private set; }
		public SiteLoginDetails LoginDetails
		{
			get
			{
				return new SiteLoginDetails(true, this.username, this.password, this.CollectedCookies);
			}
		}

		private Uri url;
		private string username;
		private string password;
		private string[] usernameMatches = { "username", "navbar_username" };
		private string[] passwordMatches = { "password", "navbar_password" };

		public ManualLoginWindow(string url, string username = "", string password = "")
		{
			InitializeComponent();

			double width = SystemParameters.PrimaryScreenWidth;
			double height = SystemParameters.PrimaryScreenHeight;

			this.url = new Uri(url, UriKind.Absolute);
			this.CollectedCookies = new CookieCollection();
			this.username = username;
			this.password = password;
			this.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
			
			// Determine window size/state
			if (width > 1024 && height > 768)
			{
				this.Width = 1024;
				this.Height = 768;
			}
			else
			{
				this.WindowState = System.Windows.WindowState.Maximized;
			}
		}

		private void ProcessCookies(string rawCookies, Encoding enc)
		{
			if (rawCookies == null || rawCookies.Length < 3) return;

			var name = 0;
			var val = 1;
			var removeEmpty = StringSplitOptions.RemoveEmptyEntries;
			var parts = rawCookies.Split(";".ToCharArray(), int.MaxValue, removeEmpty);
			var cookies = from c in parts select c.Split("=".ToCharArray(), 2, removeEmpty);

			foreach (string[] c in cookies)
			{
				this.CollectedCookies.Add(new Cookie(
					c[name].Trim("+ \t\r\n".ToCharArray()),
					c[val],
					"/",
					this.url.Host
				));
			}
		}

		private void ProcessDom()
		{
			string attr;
			Uri href;
			
			// Collect
			var objects = this.wbLogin.Document.GetElementsByTagName("object");
			var iframes = this.wbLogin.Document.GetElementsByTagName("iframe");
			var frames = this.wbLogin.Document.GetElementsByTagName("frame");
			var links = this.wbLogin.Document.Links;
			var textboxes = from wf.HtmlElement elm in this.wbLogin.Document.GetElementsByTagName("input")
							where (elm.GetAttribute("type") == "text" || elm.GetAttribute("type") == "password") && elm.Name.Length > 0
							select elm;
			
			// Remove crap
			foreach (wf.HtmlElement elm in objects) elm.OuterHtml = string.Empty;
			foreach (wf.HtmlElement elm in iframes) elm.OuterHtml = string.Empty;
			foreach (wf.HtmlElement elm in frames) elm.OuterHtml = string.Empty;

			// Fix links
			foreach (wf.HtmlElement elm in links)
			{
				attr = elm.GetAttribute("href");

				if (attr.StartsWith("http://"))
				{
					href = new Uri(attr, UriKind.Absolute);

					if (href.Host != this.url.Host)
					{
						elm.SetAttribute("href", "#");
						continue;
					}
				}

				if (elm.GetAttribute("target").ToLower() == "_blank")
				{
					elm.SetAttribute("target", "");
				}
			}

			// Fill in login fields
			foreach (wf.HtmlElement elm in textboxes)
			{
				if (this.usernameMatches.Contains(elm.Name.ToLower()))
				{
					elm.SetAttribute("value", this.username);
				}
				else if (this.passwordMatches.Contains(elm.Name.ToLower()))
				{
					elm.SetAttribute("value", this.password);
				}
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.wbLogin.ScriptErrorsSuppressed = true;
			this.wbLogin.AllowWebBrowserDrop = false;
			this.wbLogin.IsWebBrowserContextMenuEnabled = false;
			this.wbLogin.DocumentCompleted += new wf.WebBrowserDocumentCompletedEventHandler(wbLogin_DocumentCompleted);
			this.wbLogin.Navigating += new wf.WebBrowserNavigatingEventHandler(wbLogin_Navigating);
			this.wbLogin.ProgressChanged += new wf.WebBrowserProgressChangedEventHandler(wbLogin_ProgressChanged);
			this.wbLogin.Navigate(url);
		}

		private void btnLoggedIn_Click(object sender, RoutedEventArgs e)
		{
			this.wbLogin.Dispose();
			/*foreach (Cookie c in this.CollectedCookies)
			{
				MessageBox.Show(c.Name + " : " + HttpUtility.UrlDecode(c.Value), c.Domain);
			}*/
			Http.SessionCookies.Add(this.CollectedCookies);
			this.DialogResult = true;
		}

		private void wbLogin_DocumentCompleted(object sender, wf.WebBrowserDocumentCompletedEventArgs e)
		{
			this.btnLoggedIn.IsEnabled = true;
			this.ProcessDom();
			this.ProcessCookies(this.wbLogin.Document.Cookie, Encoding.GetEncoding(this.wbLogin.Document.Encoding));
			this.wbLogin.Document.Window.Focus();
		}

		private void wbLogin_Navigating(object sender, wf.WebBrowserNavigatingEventArgs e)
		{
			this.btnLoggedIn.IsEnabled = false;
		}

		private void wbLogin_ProgressChanged(object sender, wf.WebBrowserProgressChangedEventArgs e)
		{
			if (e.CurrentProgress < e.MaximumProgress)
			{
				this.Title = string.Format(
					"Manual Login [{0:N}%]",
					(double)e.CurrentProgress / ((double)e.MaximumProgress / 100)
				);
			}
			else
			{
				this.Title = "Manual Login";
			}
		}
	}
}
