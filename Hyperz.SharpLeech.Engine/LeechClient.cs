using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hyperz.SharpLeech.Engine.Html;
using Hyperz.SharpLeech.Engine.Net;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.Engine
{
	public class LeechClient
	{
		#region Public Properties
		public SiteReader Reader { get; set; }
		public SiteType PostSite { get; set; }
		/*public SiteType LeechSite
		{
			get
			{
				try { return this.Reader.Type; }
				catch { return null; }
			}
		}*/
		public LeechDirection Direction { get; set; }
		public Int32 Pause { get; set; }
		public Int32 Timeout { get; set; }
		public Int32 PostSectionId { get; set; }
		public Int32 PostIconId { get; set; }
		public Int32 LeechSectionId { get; set; }
		public Int32 StartPage { get; set; }
		public Int32 MaxPages { get; set; }
		public Boolean TitleRegex { get; set; }
		public Boolean ContentRegex { get; set; }
		public String[] TitleReplacements { get; set; }
		public String[] ContentReplacements { get; set; }
		public String PersonalMessage { get; set; }
		public MessageLocation PersonalMessageLocation { get; set; }
		public Boolean IsRunning { get; private set; }
		/*public String[] Hashes
		{
			get { return (this.Hashes == null) ? new string[0] : this._hashes.ToArray(); }
		}*/
		#endregion

		#region Private Fields
		private DateTime lastPost;
		private Int32 pagesLeeched;
		private List<String> hashBuffer;
		private Boolean stop;
		private String hashFile;
		private Thread leechThread;
		private List<string> _hashes = null;
		#endregion

		public event EventHandler Started;
		public event EventHandler Stopped;
		public event TopicReadEventHandler TopicRead;
		public event TopicUrlsReceivedEventHandler TopicUrlsReceived;
		public event ErrorEventHandler Error;
		public event ClientMessageEventHandler ClientMessage;
		
		public LeechClient()
		{
			this.hashFile = Environment.CurrentDirectory + @"\PostedTopics.txt";

			if (!File.Exists(hashFile))
			{
				try { File.Create(this.hashFile); }
				catch (Exception error) { ErrorLog.LogException(error); }
			}

			this.Reader = null;
			this.PostSite = null;
			this.Direction = LeechDirection.NewToOld;
			this.Pause = 0;
			this.Timeout = 100000;
			this.PostSectionId = 1;
			this.PostIconId = 0;
			this.LeechSectionId = 1;
			this.StartPage = 1;
			this.MaxPages = 0;
			this.TitleRegex = false;
			this.ContentRegex = false;
			this.TitleReplacements = new string[0];
			this.ContentReplacements = new string[0];
			this.PersonalMessage = String.Empty;
			this.PersonalMessageLocation = MessageLocation.Nowhere;
			this.IsRunning = false;

			this.lastPost = new DateTime(0);
			this.pagesLeeched = 0;
			this.hashBuffer = new List<string>();
			this.stop = false;
			//this.leechThread = new Thread(new ThreadStart(this.Worker));
			//this.leechThread.IsBackground = true;
		}

		public void Start()
		{
			if (this.IsRunning) return;

			this.OnClientMessage("Initializing, please wait...");
			this.leechThread = new Thread(new ThreadStart(this.Worker));
			this.leechThread.IsBackground = true;
			this.PostSite.ReadyChanged -= Type_ReadyChanged;
			this.PostSite.ReadyChanged += Type_ReadyChanged;
			this._hashes = new List<string>();

			try
			{
				string[] hashes = File.ReadAllLines(this.hashFile);
				this._hashes.AddRange(hashes);
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
			}

			Http.SessionCookies.Add(this.PostSite.User.Cookies);
			Http.SessionCookies.Add(this.Reader.Type.User.Cookies);
			Http.KeepAlive = true;
			Http.MaxRedirects = 2;
			Http.Timeout = this.Timeout;
			Http.UseCompression = false;

			this.IsRunning = true;
			this.OnStarted(this, new EventArgs());
			this.PostSite.MakeReady(this.PostSectionId);
		}

		public void Stop()
		{
			//this.PostSite.ReadyChanged -= Type_ReadyChanged;
			this.IsRunning = false;
			this.leechThread.Abort();
		}

		#region Event Related Methods
		protected virtual void OnStarted(object sender, EventArgs e)
		{
			if (this.Started != null) this.Started(sender, e);
		}

		protected virtual void OnStopped(object sender, EventArgs e)
		{
			if (this.Stopped != null) this.Stopped(sender, e);
			
			this.IsRunning = false;
			this.pagesLeeched = 0;

			try
			{
				File.WriteAllLines(this.hashFile, this._hashes.ToArray());
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
			}

			this._hashes.Clear();
			this._hashes = null;
		}

		protected virtual void OnTopicRead(object sender, TopicReadEventArgs e)
		{
			if (this.TopicRead != null && this.IsRunning) this.TopicRead(sender, e);
		}

		protected virtual void OnTopicUrlsReceived(object sender, TopicUrlsReceivedEventArgs e)
		{
			if (this.TopicUrlsReceived != null && this.IsRunning) this.TopicUrlsReceived(sender, e);
		}

		protected virtual void OnError(object sender, ErrorEventArgs e)
		{
			if (this.Error != null)
			{
				ErrorLog.LogException(e.Error);
				this.Error(sender, e);
			}
		}
		
		protected virtual void OnClientMessage(object sender, ClientMessageEventArgs e)
		{
			if (this.ClientMessage != null && this.IsRunning) this.ClientMessage(sender, e);
		}

		protected virtual void OnClientMessage(string msg)
		{
			this.OnClientMessage(this, new ClientMessageEventArgs(msg));
		}

		private void Type_ReadyChanged(object sender, EventArgs e)
		{
			if (this.PostSite.IsReady)
			{
				if (this.Pause < 1)
				{
					this.IsRunning = true;
					this.leechThread = new Thread(new ThreadStart(this.AsyncWorker));
					this.leechThread.Start();
				}
				else
				{
					this.IsRunning = true;
					this.leechThread.Start();
				}
			}
			else
			{
				this.IsRunning = false;
				this.OnClientMessage("Could not initialize. Please check your base URL/section ID.");
				this.OnStopped(this, new EventArgs());
			}
		}
		#endregion

		protected virtual void Worker()
		{
			var sw = new System.Diagnostics.Stopwatch();
			int pageStep = 1;
			int page = (this.StartPage < 1) ? 1 : this.StartPage;
			int maxPages = (this.MaxPages < 1) ? int.MaxValue : this.MaxPages;
			string[] urls = new string[0];

			while (this.pagesLeeched < maxPages && page > 0)
			{
				try
				{
					var pageReq = this.Reader.GetPage(this.LeechSectionId, page, this.Reader.TopicsPerPage);
					pageReq.CookieContainer.Add(this.Reader.Type.User.Cookies);
					var pageRes = (!this.Reader.Type.AllowRedirects) ?
						Http.Request(pageReq) : Http.HandleRedirects(Http.Request(pageReq), false);

					if (pageRes.HasError)
					{
						this.OnError(this, new ErrorEventArgs(pageRes.Error));
						page = this.PageMove(page, pageStep);
						continue;
					}

					this.OnClientMessage("Reading page #" + page + ".");

					urls = this.Reader.GetTopicUrls(pageRes.Data);

					this.OnClientMessage(String.Format("Extracted {0} topics from page #{1}.", urls.Length, page));
				}
				catch (ThreadAbortException)
				{
					this.IsRunning = false;
					this.OnStopped(this, new EventArgs());
					return;
				}
				catch (Exception error)
				{
					this.OnError(this, new ErrorEventArgs(error));
				}

				for (int i = 0; i < urls.Length; i++)
				{
					try
					{
						sw.Reset();
						sw.Start();

						this.OnClientMessage(String.Format("Reading topic #{0} from page #{1}.", i + 1, page));
						string hash = SiteTopic.GetUrlHash(urls[i]);

						if (!this._hashes.Contains(hash))
						{
							this.AddHash(hash);
							var t = this.Reader.GetTopic(urls[i]);
							this.OnTopicRead(this, new TopicReadEventArgs(t));

							if (t != null)
							{
								this.OnClientMessage(String.Format("Posting topic: {0}.", t.Title));

								var postReq = this.PostSite.CreateTopic(this.ProcessTopic(t));
								postReq.CookieContainer.Add(this.PostSite.User.Cookies);
								var postRes = (!this.Reader.Type.AllowRedirects) ?
								Http.FastRequest(postReq) : Http.HandleRedirects(Http.FastRequest(postReq), true);

								if (postRes.HasError) this.OnError(this, new ErrorEventArgs(postRes.Error));
							}
						}
						else
						{
							this.OnClientMessage("Skipping duplicate topics...");
							Thread.Sleep(300);
						}

						sw.Stop();

						if (sw.ElapsedMilliseconds < this.Pause)
						{
							var p = this.Pause - (int)sw.ElapsedMilliseconds;
							this.OnClientMessage(String.Format("Pausing for {0:N0} ms.", p));
							Thread.Sleep(p);
						}
					}
					catch (ThreadAbortException)
					{
						this.IsRunning = false;
						this.OnStopped(this, new EventArgs());
						return;
					}
					catch (Exception error)
					{
						this.OnError(this, new ErrorEventArgs(error));
					}
				}

				this.StartPage = page = this.PageMove(page, pageStep);
				this.pagesLeeched++;
			}

			this.IsRunning = false;
			this.OnClientMessage("Finished.");
			this.OnStopped(this, new EventArgs());
		}

		protected virtual void AsyncWorker()
		{
			//var sw = new System.Diagnostics.Stopwatch();
			int pageStep = 1;
			int page = (this.StartPage < 1) ? 1 : this.StartPage;
			int maxPages = (this.MaxPages < 1) ? int.MaxValue : this.MaxPages;
			string[] urls = new string[0];

			while (this.pagesLeeched < maxPages && page > 0)
			{
				try
				{
					var pageReq = this.Reader.GetPage(this.LeechSectionId, page, this.Reader.TopicsPerPage);
					pageReq.CookieContainer.Add(this.Reader.Type.User.Cookies);
					var pageRes = (!this.Reader.Type.AllowRedirects) ?
						Http.Request(pageReq) : Http.HandleRedirects(Http.Request(pageReq), false);

					if (pageRes.HasError)
					{
						this.OnError(this, new ErrorEventArgs(pageRes.Error));
						page = this.PageMove(page, pageStep);
						continue;
					}

					this.OnClientMessage("Reading page #" + page + ".");

					urls = this.Reader.GetTopicUrls(pageRes.Data);

					this.OnClientMessage(String.Format("Extracted {0} topics from page #{1}.", urls.Length, page));
				}
				catch (ThreadAbortException)
				{
					this.IsRunning = false;
					this.OnStopped(this, new EventArgs());
					return;
				}
				catch (Exception error)
				{
					this.OnError(this, new ErrorEventArgs(error));
				}

				ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = ServicePointManager.DefaultConnectionLimit };

				var plr = Parallel.For(0, urls.Length, po, (i) =>
				{
					if (!this.IsRunning) return;

					try
					{
						this.OnClientMessage(String.Format("Reading topic #{0} from page #{1}.", i + 1, page));
						string hash = SiteTopic.GetUrlHash(urls[i]);

						if (!this._hashes.Contains(hash))
						{
							this.AddHash(hash);
							var t = this.Reader.GetTopic(urls[i]);
							this.OnTopicRead(this, new TopicReadEventArgs(t));

							if (!this.IsRunning) return;

							if (t != null)
							{
								this.OnClientMessage(String.Format("Posting topic: {0}.", t.Title));

								var postReq = this.PostSite.CreateTopic(this.ProcessTopic(t));
								postReq.CookieContainer.Add(this.PostSite.User.Cookies);
								var postRes = (!this.Reader.Type.AllowRedirects) ?
								Http.FastRequest(postReq) : Http.HandleRedirects(Http.FastRequest(postReq), true);

								if (postRes.HasError) this.OnError(this, new ErrorEventArgs(postRes.Error));
							}
						}
						else
						{
							this.OnClientMessage("Skipping duplicate topics...");
						}
					}
					catch (ThreadAbortException)
					{
						this.IsRunning = false;
						this.OnStopped(this, new EventArgs());
						return;
					}
					catch (Exception error)
					{
						this.OnError(this, new ErrorEventArgs(error));
					}
				});

				/*try
				{
					while (!plr.IsCompleted) Thread.Sleep(10);
				}
				catch (ThreadAbortException)
				{
					this.IsRunning = false;
					this.OnStopped(this, new EventArgs());
					return;
				}
				catch (Exception error)
				{
					this.OnError(this, new ErrorEventArgs(error));
				}*/

				this.StartPage = page = this.PageMove(page, pageStep);
				this.pagesLeeched++;
			}

			this.IsRunning = false;
			this.OnClientMessage("Finished.");
			this.OnStopped(this, new EventArgs());
		}

		private int PageMove(int currentPage, int pageStep)
		{
			switch (this.Direction)
			{
				case LeechDirection.OldToNew:
					return currentPage - pageStep;

				case LeechDirection.NewToOld:
				default:
					return currentPage + pageStep;
			}
		}

		private void AddHash(string hash)
		{
			if (this._hashes != null && !this._hashes.Contains(hash))
			{
				this._hashes.Add(hash);
			}
		}

		private SiteTopic ProcessTopic(SiteTopic topic)
		{
			topic.SectionId = this.PostSectionId;
			topic.IconId = this.PostIconId;

			if (this.TitleRegex)
			{
				foreach (string pattern in this.TitleReplacements)
				{
					try
					{
						topic.Title = Regex.Replace(topic.Title, pattern.Trim(), String.Empty);
					}
					catch { }
				}
			}

			if (this.ContentRegex)
			{
				foreach (string pattern in this.ContentReplacements)
				{
					try
					{
						topic.Content = Regex.Replace(topic.Content, pattern.Trim(), String.Empty);
					}
					catch { }
				}
			}

			switch (this.PersonalMessageLocation)
			{
				case MessageLocation.Bottom:
					topic.Content += "\n" + this.PersonalMessage;
					break;

				case MessageLocation.Top:
					topic.Content = this.PersonalMessage + "\n" + topic.Content;
					break;

				case MessageLocation.Nowhere:
				default:
					break;
			}

			return topic;
		}
	}

	#region Event Handlers & Arguments
	public class TopicReadEventArgs : EventArgs
	{
		public SiteTopic Topic { get; private set; }
		public Boolean HasTopic { get { return (this.Topic != null); } }

		public TopicReadEventArgs(SiteTopic topic)
		{
			this.Topic = topic;
		}
	}

	public class TopicUrlsReceivedEventArgs : EventArgs
	{
		public String[] Urls { get; private set; }
		public Boolean HasUrls { get { return (this.Urls != null && this.Urls.Length > 0); } }

		public TopicUrlsReceivedEventArgs(string[] urls)
		{
			this.Urls = urls;
		}
	}

	public class ErrorEventArgs : EventArgs
	{
		public Exception Error { get; private set; }
		public Boolean HasError { get { return (this.Error != null); } }

		public ErrorEventArgs(Exception error)
		{
			this.Error = error;
		}
	}

	public class ClientMessageEventArgs : EventArgs
	{
		public String Message { get; private set; }

		public ClientMessageEventArgs(String message)
		{
			this.Message = message;
		}
	}

	public delegate void TopicReadEventHandler(object sender, TopicReadEventArgs e);
	public delegate void TopicUrlsReceivedEventHandler(object sender, TopicUrlsReceivedEventArgs e);
	public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
	public delegate void ClientMessageEventHandler(object sender, ClientMessageEventArgs e);
	#endregion
}
