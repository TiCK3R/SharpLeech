/*using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Hyperz.SharpLeech.Engine;
using Hyperz.SharpLeech.Engine.Html;
using Hyperz.SharpLeech.Engine.Net;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.SiteReaders
{
	internal class Test : SiteReader
	{
		public Test(string siteName, string baseUrl, string siteTypeName, int topicsPerPage, Dictionary<string, int> sections,
					string defaultEncoding = "", bool allowRedirects = false, bool useFriendlyLinks = false)
			: base(siteName, baseUrl, siteTypeName, topicsPerPage, sections, defaultEncoding, allowRedirects, useFriendlyLinks)
		{ }

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			return base.GetPage(sectionId, page, siteTopicsPerPage);
		}

		public override SiteTopic GetTopic(int topicId)
		{
			return base.GetTopic(topicId);
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.Type.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.Type.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//img[@alt='Reply With Quote']");
				string link = HttpUtility.HtmlDecode(nodes[0].ParentNode.GetAttributeValue("href", String.Empty));

				nodes = doc.DocumentNode.SelectNodes("//a[@title='Reload this Page']");
				string title = HttpUtility.HtmlDecode(nodes[0].InnerText).Trim();

				req = Http.Prepare(this.Type.BaseUrl + "/" + link);
				req.Method = "GET";
				req.Referer = url;

				result = this.Type.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				string content = doc.DocumentNode.SelectNodes("//textarea[@name='message']")[0].InnerText;

				content = HttpUtility.HtmlDecode(content.Substring(content.IndexOf(']') + 1)).Trim();
				content = content.Substring(0, content.Length - "[/quote]".Length);

				return new SiteTopic(
					title.Trim(),
					content.Trim(),
					0, 0, url
				);
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return null;
			}
		}

		public override string[] GetTopicUrls(string html)
		{
			return base.GetTopicUrls(html);
		}

		protected override void Init()
		{
			base.Init();
		}

		public override void LoginUser(string username, string password)
		{
			base.LoginUser(username, password);
		}

		public override void LogoutUser()
		{
			base.LogoutUser();
		}

		public override void MakeReady(int sectionId)
		{
			base.MakeReady(sectionId);
		}
	}
}

*/