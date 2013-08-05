using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Hyperz.SharpLeech.Engine.Net;

namespace Hyperz.SharpLeech.Engine
{
	public abstract class SiteType
	{
		private string baseUrl;

		public string Name { get; set; }
		public virtual string LoginPath { get; set; }
		public virtual string NewTopicPath { get; set; }
		public virtual string PagePath { get; set; }
		public virtual string TopicPath { get; set; }
		public virtual string RegisterPath { get; set; }
		public bool AllowRedirects { get; set; }
		public bool UseFriendlyLinks { get; set; }
		public SiteTypeDetails Details { get; set; }
		public Encoding SiteEncoding { get; set; }
		public SiteLoginDetails User { get; set; }
		public Dictionary<string, string> TemplateReplacements { get; set; }

		public virtual string BaseUrl
		{
			get { return this.baseUrl; }
			set { this.baseUrl = value.TrimEnd(@"\/".ToCharArray()); }
		}

		public virtual bool IsReady
		{
			get
			{
				if (this.User == null || this.TemplateReplacements == null) return false;
				else return (this.User.IsLoggedIn && this.TemplateReplacements.Count > 0);
			}
		}

		public event LoginEventHandler Login;
		public event EventHandler ReadyChanged;
		public event TopicPostedEventHandler TopicPosted;

		public SiteType CreateInstance()
		{
			return Activator.CreateInstance(this.GetType()) as SiteType;
		}

		public abstract void LoginUser(string username, string password);

		public abstract void LogoutUser();

		public abstract void MakeReady(int sectionId);

		public abstract HttpWebRequest CreateTopic(SiteTopic topic);

		public abstract HttpWebRequest[] CreateTopics(SiteTopic[] topics);

		public abstract HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage);

		public abstract SiteTopic GetTopic(string url);

		public abstract SiteTopic GetTopic(int topicId);

		public abstract string[] GetTopicUrls(string html);

		protected virtual void OnLogin(object sender, LoginEventArgs e)
		{
			if (this.Login != null) this.Login(sender, e);
		}

		protected virtual void OnReadyChanged(object sender, EventArgs e)
		{
			if (this.ReadyChanged != null) this.ReadyChanged(sender, e);
		}

		protected virtual void OnTopicPosted(object sender, TopicPostedEventArgs e)
		{
			if (this.TopicPosted != null) this.TopicPosted(sender, e);
		}

		public new virtual string ToString()
		{
		 	 return this.Name;
		}
	}

	public class LoginEventArgs : EventArgs
	{
		public SiteLoginDetails LoginDetails { get; private set; }

		public bool LoggedIn
		{
			get { return this.LoginDetails.IsLoggedIn; }
		}

		public LoginEventArgs(SiteLoginDetails loginDetails)
		{
			this.LoginDetails = loginDetails;
		}
	}

	public class TopicPostedEventArgs : EventArgs
	{
		public SiteTopic Topic { get; private set; }
		public HttpResult Result { get; private set; }

		public TopicPostedEventArgs(SiteTopic topic, HttpResult result)
		{
			this.Topic = topic;
			this.Result = result;
		}
	}

	public delegate void LoginEventHandler(object sender, LoginEventArgs e);
	public delegate void TopicPostedEventHandler(object sender, TopicPostedEventArgs e);
}
