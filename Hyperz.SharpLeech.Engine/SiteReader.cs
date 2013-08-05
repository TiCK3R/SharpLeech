using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Hyperz.SharpLeech.Engine.Html;
using Hyperz.SharpLeech.Engine.Net;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.Engine
{
	public class SiteReader
	{
		public string SiteName { get; set; }
		public int TopicsPerPage { get; set; }
		public Dictionary<string, int> Sections { get; set; }
		public SiteType Type { get; set; }
		public string DefaultEncoding
		{
			get { return this.Type.SiteEncoding.BodyName; }
			set { this.Type.SiteEncoding = Encoding.GetEncoding(value); }
		}

		public SiteReader(string siteName, string baseUrl, string siteTypeName, int topicsPerPage, Dictionary<string, int> sections,
						  string defaultEncoding = "", bool allowRedirects = false, bool useFriendlyLinks = false)
		{
			SiteType siteType = DefaultSiteTypes.ByName(siteTypeName).CreateInstance();
			Uri uri;

			if (siteName == null)
			{
				throw new NullReferenceException("siteName cannot be null.");
			}
			else if (sections == null)
			{
				throw new NullReferenceException("sections cannot be null.");
			}
			else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out uri))
			{
				throw new ArgumentOutOfRangeException(
					"baseUrl", baseUrl,
					"The provided base URL is invalid."
				);
			}

			this.SiteName = siteName;
			this.Sections = sections;
			this.TopicsPerPage = (topicsPerPage == null || topicsPerPage < 1) ? 20 : topicsPerPage;
			this.Type = siteType;
			this.Type.BaseUrl = baseUrl;
			this.Type.AllowRedirects = allowRedirects;
			this.Type.UseFriendlyLinks = useFriendlyLinks;

			if (defaultEncoding != null || defaultEncoding != String.Empty)
			{
				this.Type.SiteEncoding = Encoding.GetEncoding(defaultEncoding);
			}

			this.Init();
		}

		public SiteReader CreateInstance()
		{
			Object[] args = new Object[] {
				this.SiteName,
				this.Type.BaseUrl,
				this.Type.Name,
				this.TopicsPerPage,
				this.Sections,
				this.DefaultEncoding,
				this.Type.AllowRedirects,
				this.Type.UseFriendlyLinks
			};

			return (SiteReader)Activator.CreateInstance(this.GetType(), args);
		}

		public virtual void LoginUser(string username, string password)
		{
			this.Type.LoginUser(username, password);
		}

		public virtual void LogoutUser()
		{
			this.Type.LogoutUser();
		}

		public virtual void MakeReady(int sectionId)
		{
			this.Type.MakeReady(sectionId);
		}

		public virtual HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			return this.Type.GetPage(sectionId, page, siteTopicsPerPage);
		}

		public virtual SiteTopic GetTopic(string url)
		{
			return this.Type.GetTopic(url);
		}

		public virtual SiteTopic GetTopic(int topicId)
		{
			return this.Type.GetTopic(topicId);
		}

		public virtual string[] GetTopicUrls(string html)
		{
			return this.Type.GetTopicUrls(html);
		}

		protected virtual void Init() { }

		public override string ToString()
		{
			return this.SiteName;
		}
	}
}
