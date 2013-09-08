using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Hyperz.SharpLeech.Engine.Html;
using Hyperz.SharpLeech.Engine.Net;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.Engine
{
	public sealed class DefaultSiteTypes
	{
		public static SiteType[] SiteTypes
		{
			get { return _siteTypes.ToArray(); }
		}
		
		private static List<SiteType> _siteTypes;

		static DefaultSiteTypes()
		{
			_siteTypes = new List<SiteType>();
			_siteTypes.Add(new SiteType_IPBoard_200());		// IPB 2
			_siteTypes.Add(new SiteType_IPBoard_300());		// IPB 3
			_siteTypes.Add(new SiteType_IPBoard_314());		// IPB 3.1.4+
			_siteTypes.Add(new SiteType_IPBoard_340());		// IPB 3.4.x
			_siteTypes.Add(new SiteType_vBulletin_300());	// vB 3
			_siteTypes.Add(new SiteType_vBulletin_400());	// vB 4
			_siteTypes.Add(new SiteType_phpBB_200());		// phpBB 2
			_siteTypes.Add(new SiteType_phpBB_300());		// phpBB 3
		}

		public static SiteType ByName(string name)
		{
			var query = (from site in _siteTypes
						 where site.Name.ToLower() == name.ToLower()
						 select site).ToArray();
			
			return (query.Length > 0) ? query[0] : null;
		}

		public static string[] GetNames()
		{
			var names = from site in _siteTypes
						select site.Name;

			return names.ToArray();
		}

		public static void SetBaseUrls(string url)
		{
			for (int i = 0; i < _siteTypes.Count; i++) _siteTypes[i].BaseUrl = url;
		}
	}

	#region IPBoard
	internal class SiteType_IPBoard_200 : SiteType
	{
		public override string LoginPath
		{
			get
			{
				if (this.UseFriendlyLinks)
				{
					return "/login.html&CODE=01";
				}
				else
				{
					return "/index.php?act=Login&CODE=01";
				}
			}
		}

		public override string NewTopicPath
		{
			get
			{
				if (this.UseFriendlyLinks)
				{
					return "/start-new-topic-f{0}.html";
				}
				else
				{
					return "/index.php?act=post&do=new_post&f={0}";
				}
			}
		}

		public override string PagePath
		{
			get
			{
				if (this.UseFriendlyLinks)
				{
					return "/null-f{0}.html&st={1}";
				}
				else
				{
					return "/index.php?showforum={0}&prune_day=100&sort_by=Z-A&sort_key=last_post&topicfilter=all&st={1}";
				}
			}
		}

		public override string TopicPath
		{
			get
			{
				if (this.UseFriendlyLinks)
				{
					return "/null-f{0}.html";
				}
				else
				{
					return "/index.php?showtopic={0}";
				}
			}
		}

		public override string RegisterPath
		{
			get
			{
				if (this.UseFriendlyLinks)
				{
					return "/register.html";
				}
				else
				{
					return "/index.php?act=Reg&CODE=00";
				}
			}
		}

		public SiteType_IPBoard_200()
		{
			this.Name = "IP.Board 2.x.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.IPBoard_200_ContentTemplate, Res.IPBoard_200_ContentType);
			//this.LoginPath = "/index.php?act=Login&CODE=01";
			//this.NewTopicPath = "/index.php?act=post&do=new_post&f={0}";
			//this.PagePath = "/index.php?showforum={0}&prune_day=100&sort_by=Z-A&sort_key=last_post&topicfilter=all&st={1}";
			//this.TopicPath = "/index.php?showtopic={0}";
			//this.RegisterPath = "/index.php?act=Reg&CODE=00";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("ISO-8859-1");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.IPBoard_200_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding)
				);
				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				int parse = -1;

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;
				req.AllowAutoRedirect = false;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 2)
				{
					ErrorLog.LogException(result.Error);

					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("member_id"))
					{
						if (c.Value.Length > 0 && int.TryParse(c.Value, out parse) && parse > 0) check++;
						//else if (c.Value.Length > 14 && c.Value.Trim().EndsWith(".")) check++;
					}
					else if (c.Name.EndsWith("pass_hash"))
					{
						if (c.Value.Length > 1) check++;
					}
				}

				if (check > 1)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);

					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "auth_key":
							replacements.Add("[auth_key]", n.Attributes["value"].Value);
							break;

						case "attach_post_key":
							replacements.Add("[attach_post_key]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (replacements.Count != 2) replacements.Clear();

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url = this.BaseUrl + "/index.php";
			string data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;
			
			req.Method = "POST";
			req.ContentType = Res.IPBoard_200_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				url = this.BaseUrl + "/index.php";
				data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.IPBoard_200_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;
				//req.AllowAutoRedirect = false;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page > 1) ? page - 1 : 0;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page * siteTopicsPerPage);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a[@title='Reply directly to this post']");
				string link = HttpUtility.HtmlDecode(nodes[0].GetAttributeValue("href", String.Empty));

				req = Http.Prepare(link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);
				
				string title = (from n in doc.DocumentNode.SelectNodes("//td[@class='maintitle']")
								where n.InnerText.Trim().Contains("Replying to ")
								select n.InnerText.Replace("Replying to ", "")).ToArray()[0];
				string content = doc.DocumentNode.SelectNodes("//textarea[@name='Post']")[0].InnerText;

				content = HttpUtility.HtmlDecode(content.Substring(content.IndexOf(']') + 1)).Trim();
				content = content.Substring(0, content.Length - "[/quote]".Length);

				return new SiteTopic(
					HttpUtility.HtmlDecode(title).Trim(),
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var allowAdd = true;
			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;
			Uri uri;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//table");
				var table = (from n in nodes
							 where n.InnerHtml.Contains("id=\"tid-link-") &&
								   n.InnerHtml.Contains("topic_toggle_folder") &&
								   n.InnerHtml.Contains("<!-- Begin Topic Entry ")
							 select n).ToArray();

				//throw new Exception(table[table.Length - 1].SelectNodes(".//td[1]").Count.ToString());

				foreach (var n in table[table.Length - 1].SelectNodes(".//td[1]"))
				{
					if (n.InnerHtml.Contains("<b>Forum Topics</b>"))
					{
						allowAdd = false;
						break;
					}
				}

				foreach (var n in table[table.Length - 1].SelectNodes(".//tr"))
				{
					if (!allowAdd)
					{
						nodes = n.SelectNodes(".//td[1]");

						if (nodes.Count > 0 && nodes[0].InnerHtml.Contains("<b>Forum Topics</b>"))
						{
							allowAdd = true;
						}
					}
					else
					{
						nodes = n.SelectNodes(".//a");
						
						if (nodes.Count > 0)
						{
							var links = (from link in nodes
										 where link.GetAttributeValue("id", "").StartsWith("tid-link-") &&
											   link.GetAttributeValue("href", "").StartsWith("http:")
										 select HttpUtility.HtmlDecode(link.GetAttributeValue("href", ""))).
										 ToArray();

							if (links.Length > 0 && Uri.TryCreate(links[0].Trim(), UriKind.Absolute, out uri))
							{
								urls.Add(links[0].Trim());
							}
						}
					}
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}

	internal class SiteType_IPBoard_300 : SiteType
	{
		public override string PagePath
		{
			get
			{
				return (this.UseFriendlyLinks) ? base.PagePath : "/index.php?" + base.PagePath;
			}
			set
			{
				base.PagePath = value;
			}
		}

		public override string TopicPath
		{
			get
			{
				return (this.UseFriendlyLinks) ? base.TopicPath : "/index.php?" + base.TopicPath;
			}
			set
			{
				base.TopicPath = value;
			}
		}

		public SiteType_IPBoard_300()
		{
			this.Name = "IP.Board 3.x.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.IPBoard_300_ContentTemplate, Res.IPBoard_300_ContentType);
			this.LoginPath = "/index.php?app=core&module=global&section=login&do=process";
			this.NewTopicPath = "/index.php?app=forums&module=post&section=post&do=new_post&f={0}";
			this.PagePath = "/forum/{0}-null/page__prune_day__100__sort_by__Z-A__sort_key__last_post__topicfilter__all__st__{1}";
			this.TopicPath = "/topic/{0}-null/";
			this.RegisterPath = "/index.php?app=core&module=global&section=register";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("ISO-8859-1");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.IPBoard_300_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding)
				);
				data = "referer=" + HttpUtility.UrlEncode(this.BaseUrl + "/index.php?", this.SiteEncoding) + "&" + data;
				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				int parse = -1;

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 2)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("member_id"))
					{
						if (c.Value.Length > 0 && int.TryParse(c.Value, out parse) && parse > 0) check++;
					}
					else if (c.Name.EndsWith("pass_hash"))
					{
						if (c.Value.Length > 1) check++;
					}
				}

				if (check > 1)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "auth_key":
							replacements.Add("[auth_key]", n.Attributes["value"].Value);
							break;

						case "attach_post_key":
							replacements.Add("[attach_post_key]", n.Attributes["value"].Value);
							break;

						case "s":
							replacements.Add("[s]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (replacements.Count != 3) replacements.Clear();

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url = this.BaseUrl + "/index.php";
			string data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.IPBoard_300_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;
			//req.AllowAutoRedirect = false;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				url = this.BaseUrl + "/index.php";
				data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.IPBoard_300_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;
				//req.AllowAutoRedirect = false;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page > 1) ? page - 1 : 0;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page * siteTopicsPerPage);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a[@title='Reply directly to this post']");
				string link = HttpUtility.HtmlDecode(nodes[0].GetAttributeValue("href", String.Empty));

				req = Http.Prepare(link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				string title = (from n in doc.DocumentNode.SelectNodes("//h2[@class='maintitle']")
								where n.InnerText.Trim().Contains("Replying to ")
								select n.InnerText.Replace("Replying to ", "")).ToArray()[0];
				string content = doc.DocumentNode.SelectNodes("//textarea[@name='Post']")[0].InnerText;

				content = HttpUtility.HtmlDecode(content.Substring(content.IndexOf(']') + 1)).Trim();
				content = content.Substring(0, content.Length - "[/quote]".Length);

				// Fix IPB3 quotes
				string pattern = @"(?i)\[quote [\w\d " + '"' + @"'-=]+\]";
				string replace = "[quote]";

				content = Regex.Replace(content, pattern, replace);

				return new SiteTopic(
					HttpUtility.HtmlDecode(title).Trim(),
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;
			Uri uri;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//a[@class='topic_title']");
				var anchors = from n in nodes
							  where !n.ParentNode.InnerHtml.Contains("class=\"topic_prefix\"") &&
									n.GetAttributeValue("id", "").StartsWith("tid-link-") &&
									n.GetAttributeValue("href", "").StartsWith("http:")
							  select HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Trim();

				//throw new Exception(anchors.ToArray().Length.ToString());

				foreach (string a in anchors)
				{
					if (Uri.TryCreate(a, UriKind.Absolute, out uri)) urls.Add(a);
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}

	internal class SiteType_IPBoard_314 : SiteType
	{
		public override string PagePath
		{
			get
			{
				return (this.UseFriendlyLinks) ? base.PagePath : "/index.php?" + base.PagePath;
			}
			set
			{
				base.PagePath = value;
			}
		}

		public override string TopicPath
		{
			get
			{
				return (this.UseFriendlyLinks) ? base.TopicPath : "/index.php?" + base.TopicPath;
			}
			set
			{
				base.TopicPath = value;
			}
		}

		public SiteType_IPBoard_314()
		{
			this.Name = "IP.Board 3.1.4+";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.IPBoard_300_ContentTemplate, Res.IPBoard_300_ContentType);
			this.LoginPath = "/index.php?app=core&module=global&section=login&do=process";
			this.NewTopicPath = "/index.php?app=forums&module=post&section=post&do=new_post&f={0}";
			this.PagePath = "/forum/{0}-null/page__prune_day__100__sort_by__Z-A__sort_key__last_post__topicfilter__all__st__{1}";
			this.TopicPath = "/topic/{0}-null/";
			this.RegisterPath = "/index.php?app=core&module=global&section=register";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("ISO-8859-1");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				var loginPath = "/index.php?app=core&module=global&section=login";
				var html = Http.Get(this.BaseUrl + loginPath).Data;
				var doc = new HtmlDocument();

				doc.LoadHtml(html);

				var auth = doc.DocumentNode.SelectSingleNode("//input[@name='auth_key']");


				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.IPBoard_314_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding),
					auth.GetAttributeValue("value", ""),
					HttpUtility.UrlEncode(this.BaseUrl + loginPath, this.SiteEncoding)
				);

				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				int parse = -1;

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 2)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("member_id"))
					{
						if (c.Value.Length > 0 && int.TryParse(c.Value, out parse) && parse > 0) check++;
					}
					else if (c.Name.EndsWith("pass_hash"))
					{
						if (c.Value.Length > 1) check++;
					}
				}

				if (check > 1)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "auth_key":
							if (!replacements.ContainsKey("[auth_key]"))
								replacements.Add("[auth_key]", n.Attributes["value"].Value);
							break;

						case "attach_post_key":
							if (!replacements.ContainsKey("[attach_post_key]"))
								replacements.Add("[attach_post_key]", n.Attributes["value"].Value);
							break;

						case "s":
							if (!replacements.ContainsKey("[s]"))
								replacements.Add("[s]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (replacements.Count != 3) replacements.Clear();

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url = this.BaseUrl + "/index.php";
			string data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.IPBoard_300_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;
			//req.AllowAutoRedirect = false;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				url = this.BaseUrl + "/index.php";
				data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.IPBoard_300_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;
				//req.AllowAutoRedirect = false;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page > 1) ? page - 1 : 0;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page * siteTopicsPerPage);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a[@title='Reply directly to this post']");
				string link = HttpUtility.HtmlDecode(nodes[0].GetAttributeValue("href", String.Empty));

				req = Http.Prepare(link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				string title = (from n in doc.DocumentNode.SelectNodes("//h2[@class='maintitle']")
								where n.InnerText.Trim().Contains("Replying to ")
								select n.InnerText.Replace("Replying to ", "")).ToArray()[0];
				string content = doc.DocumentNode.SelectNodes("//textarea[@name='Post']")[0].InnerText;

				content = HttpUtility.HtmlDecode(content.Substring(content.IndexOf(']') + 1)).Trim();
				content = content.Substring(0, content.Length - "[/quote]".Length);

				// Fix IPB3 quotes
				string pattern = @"(?i)\[quote [\w\d " + '"' + @"'-=]+\]";
				string replace = "[quote]";

				content = Regex.Replace(content, pattern, replace);

				return new SiteTopic(
					HttpUtility.HtmlDecode(title).Trim(),
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;
			Uri uri;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//a[@class='topic_title']");
				var anchors = from n in nodes
							  where !n.ParentNode.InnerHtml.Contains("class=\"topic_prefix\"") &&
									n.GetAttributeValue("id", "").StartsWith("tid-link-") &&
									n.GetAttributeValue("href", "").StartsWith("http:")
							  select HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Trim();

				//throw new Exception(anchors.ToArray().Length.ToString());

				foreach (string a in anchors)
				{
					if (Uri.TryCreate(a, UriKind.Absolute, out uri)) urls.Add(a);
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}

	internal class SiteType_IPBoard_340 : SiteType
	{
		public override string PagePath
		{
			get
			{
				return (this.UseFriendlyLinks) ? base.PagePath : "/index.php?" + base.PagePath;
			}
			set
			{
				base.PagePath = value;
			}
		}

		public override string TopicPath
		{
			get
			{
				return (this.UseFriendlyLinks) ? base.TopicPath : "/index.php?" + base.TopicPath;
			}
			set
			{
				base.TopicPath = value;
			}
		}

		public SiteType_IPBoard_340()
		{
			this.Name = "IP.Board 3.4.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.IPBoard_300_ContentTemplate, Res.IPBoard_300_ContentType);
			this.LoginPath = "/index.php?app=core&module=global&section=login&do=process";
			this.NewTopicPath = "/index.php?app=forums&module=post&section=post&do=new_post&f={0}";
			this.PagePath = "/forum/{0}-null/page__prune_day__100__sort_by__Z-A__sort_key__last_post__topicfilter__all__st__{1}";
			this.TopicPath = "/topic/{0}-null/";
			this.RegisterPath = "/index.php?app=core&module=global&section=register";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("UTF-8");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				var loginPath = "/index.php?app=core&module=global&section=login";
				var html = Http.Get(this.BaseUrl + loginPath).Data;
				var doc = new HtmlDocument();

				doc.LoadHtml(html);

				var auth = doc.DocumentNode.SelectSingleNode("//input[@name='auth_key']");


				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.IPBoard_314_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding),
					auth.GetAttributeValue("value", ""),
					HttpUtility.UrlEncode(this.BaseUrl + loginPath, this.SiteEncoding)
				);

				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				int parse = -1;

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), true) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 2)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("member_id"))
					{
						if (c.Value.Length > 0 && int.TryParse(c.Value, out parse) && parse > 0) check++;
					}
					else if (c.Name.EndsWith("pass_hash"))
					{
						if (c.Value.Length > 1) check++;
					}
				}

				if (check > 1)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), false) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "auth_key":
							if (!replacements.ContainsKey("[auth_key]"))
								replacements.Add("[auth_key]", n.Attributes["value"].Value);
							break;

						case "attach_post_key":
							if (!replacements.ContainsKey("[attach_post_key]"))
								replacements.Add("[attach_post_key]", n.Attributes["value"].Value);
							break;

						case "s":
							if (!replacements.ContainsKey("[s]"))
								replacements.Add("[s]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (replacements.Count != 3) replacements.Clear();

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url = this.BaseUrl + "/index.php";
			string data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.IPBoard_300_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;
			//req.AllowAutoRedirect = false;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				url = this.BaseUrl + "/index.php";
				data = this.Details.Format(topic, this.TemplateReplacements).Replace("\r", "");
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.IPBoard_300_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;
				//req.AllowAutoRedirect = false;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page > 1) ? page - 1 : 0;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page * siteTopicsPerPage);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a[@title='Reply directly to this post']");
				string link = HttpUtility.HtmlDecode(nodes[0].GetAttributeValue("href", String.Empty));

				req = Http.Prepare(link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				string title = (from n in doc.DocumentNode.SelectNodes("//h1[@class='ipsType_pagetitle']")
								where n.InnerText.Trim().Contains("Replying to ")
								select n.InnerText.Replace("Replying to ", "")).ToArray()[0];
				string content = doc.DocumentNode.SelectNodes("//textarea[@name='Post']")[0].InnerText;

				content = HttpUtility.HtmlDecode(content.Substring(content.IndexOf(']') + 1)).Trim();
				content = content.Substring(0, content.Length - "[/quote]".Length);

				// Fix IPB3 quotes
				string pattern = @"(?i)\[quote [\w\d " + '"' + @"'-=]+\]";
				string replace = "[quote]";

				content = Regex.Replace(content, pattern, replace);

				return new SiteTopic(
					HttpUtility.HtmlDecode(title).Trim(),
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;
			Uri uri;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//a[@class='topic_title']");
				var anchors = from n in nodes
							  where !n.ParentNode.InnerHtml.Contains("class=\"topic_prefix\"") &&
									n.GetAttributeValue("id", "").StartsWith("tid-link-") &&
									n.GetAttributeValue("href", "").StartsWith("http:")
							  select HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Trim();

				//throw new Exception(anchors.ToArray().Length.ToString());

				foreach (string a in anchors)
				{
					if (Uri.TryCreate(a, UriKind.Absolute, out uri)) urls.Add(a);
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}
	#endregion

	#region vBulletin
	internal class SiteType_vBulletin_300 : SiteType
	{
		public SiteType_vBulletin_300()
		{
			this.Name = "vBulletin 3.x.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.vBulletin_300_ContentTemplate, Res.vBulletin_300_ContentType);
			this.LoginPath = "/login.php?do=login";
			this.NewTopicPath = "/newthread.php?do=newthread&f={0}";
			this.PagePath = "/forumdisplay.php?f={0}&order=desc&page={1}";
			this.TopicPath = "/showthread.php?t={0}";
			this.RegisterPath = "/register.php";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("ISO-8859-1");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.vBulletin_300_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding)
				);
				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				int parse = -1;

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 2)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("userid"))
					{
						if (c.Value.Length > 0 && int.TryParse(c.Value, out parse) && parse > 0) check++;
					}
					else if (c.Name.EndsWith("password"))
					{
						if (c.Value.Length > 1) check++;
					}
				}

				if (check > 1)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "securitytoken":
							if (replacements.Keys.Contains("[securitytoken]")) break;
							replacements.Add("[securitytoken]", n.Attributes["value"].Value);
							break;

						case "loggedinuser":
							if (replacements.Keys.Contains("[loggedinuser]")) break;
							replacements.Add("[loggedinuser]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (replacements.Count != 2) replacements.Clear();

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			topic.Title = HttpUtility.UrlEncode(topic.Title, this.SiteEncoding);
			topic.Content = HttpUtility.UrlEncode(topic.Content, this.SiteEncoding);

			string url = this.BaseUrl + "/newthread.php?do=postthread&f=" + topic.SectionId;
			string data = this.Details.Format(topic, this.TemplateReplacements);
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.vBulletin_300_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				topic.Title = HttpUtility.UrlEncode(topic.Title, this.SiteEncoding);
				topic.Content = HttpUtility.UrlEncode(topic.Content, this.SiteEncoding);

				url = this.BaseUrl + "/newthread.php?do=postthread&f=" + topic.SectionId;
				data = this.Details.Format(topic, this.TemplateReplacements);
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.vBulletin_300_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page < 1) ? 1 : page;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//img[@alt='Reply With Quote']");
				string link = HttpUtility.HtmlDecode(nodes[0].ParentNode.GetAttributeValue("href", String.Empty));

				nodes = doc.DocumentNode.SelectNodes("//div[@class='smallfont']");
				string title = (from n in nodes
								where n.ParentNode.InnerHtml.Contains("<!-- icon and title -->")
								select HttpUtility.HtmlDecode(n.InnerText).Trim()).ToArray()[0];

				req = Http.Prepare((link.StartsWith("http:")) ? link : this.BaseUrl + "/" + link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//a");
				var anchors = from n in nodes
							  where n.GetAttributeValue("id", "").StartsWith("thread_title_") &&
									!n.ParentNode.InnerHtml.Contains("Sticky:")
							  select HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Trim();

				foreach (string a in anchors)
				{
					if (a.Length > 0) urls.Add((a.StartsWith("http:")) ? a : this.BaseUrl + "/" + a);
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}

	internal class SiteType_vBulletin_400 : SiteType
	{
		public SiteType_vBulletin_400()
		{
			this.Name = "vBulletin 4.x.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.vBulletin_400_ContentTemplate, Res.vBulletin_400_ContentType);
			this.LoginPath = "/login.php?do=login";
			this.NewTopicPath = "/newthread.php?do=newthread&f={0}";
			this.PagePath = "/forumdisplay.php?f={0}&order=desc&page={1}";
			this.TopicPath = "/showthread.php?t={0}";
			this.RegisterPath = "/register.php";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("ISO-8859-1");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				byte[] rawData, hash, hashUtf8 = null;
				string data = null;
				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				int check = 0;
				int parse = -1;

				using (var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider())
				{
					hash = md5.ComputeHash(Encoding.ASCII.GetBytes(password));
					hashUtf8 = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
				}

				data = String.Format(
					Res.vBulletin_400_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding),
					BitConverter.ToString(hash).Replace("-", "").ToLower(),
					BitConverter.ToString(hashUtf8).Replace("-", "").ToLower()
				);

				rawData = this.SiteEncoding.GetBytes(data);

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);
				
				// Did the request fail?
				if (result.HasError)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("userid"))
					{
						if (c.Value.Length > 0 && int.TryParse(c.Value, out parse) && parse > 0) check++;
					}
					else if (c.Name.EndsWith("password"))
					{
						if (c.Value.Length > 1) check++;
					}
				}

				if (check > 1)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "securitytoken":
							if (replacements.Keys.Contains("[securitytoken]")) break;
							replacements.Add("[securitytoken]", n.Attributes["value"].Value);
							break;

						case "loggedinuser":
							if (replacements.Keys.Contains("[loggedinuser]")) break;
							replacements.Add("[loggedinuser]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (replacements.Count != 2) replacements.Clear();

				Http.SessionCookies.Add(this.User.Cookies);

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			topic.Title = HttpUtility.UrlEncode(topic.Title, this.SiteEncoding);
			topic.Content = HttpUtility.UrlEncode(topic.Content, this.SiteEncoding);

			string url = this.BaseUrl + "/newthread.php?do=postthread&f=" + topic.SectionId;
			string data = this.Details.Format(topic, this.TemplateReplacements);
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.vBulletin_400_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				topic.Title = HttpUtility.UrlEncode(topic.Title, this.SiteEncoding);
				topic.Content = HttpUtility.UrlEncode(topic.Content, this.SiteEncoding);

				url = this.BaseUrl + "/newthread.php?do=postthread&f=" + topic.SectionId;
				data = this.Details.Format(topic, this.TemplateReplacements);
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.vBulletin_400_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page < 1) ? 1 : page;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//img[@alt='Reply With Quote']");
				string link = HttpUtility.HtmlDecode(nodes[0].ParentNode.GetAttributeValue("href", String.Empty));

				nodes = doc.DocumentNode.SelectNodes("//span[@class='threadtitle']");
				string title = HttpUtility.HtmlDecode(nodes[0].InnerText).Trim();

				req = Http.Prepare((link.StartsWith("http:")) ? link : this.BaseUrl + "/" + link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//a");
				var anchors = from n in nodes
							  where n.GetAttributeValue("id", "").StartsWith("thread_title_") &&
									!n.ParentNode.InnerHtml.Contains("Sticky:")
							  select HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Trim();

				//throw new Exception(anchors.ToArray().Length.ToString());

				foreach (string a in anchors)
				{
					if (a.Length > 0) urls.Add((a.StartsWith("http:")) ? a : this.BaseUrl + "/" + a);
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}
	#endregion

	#region phpBB
	internal class SiteType_phpBB_200 : SiteType
	{
		public SiteType_phpBB_200()
		{
			this.Name = "phpBB 2.x.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.phpBB_200_ContentTemplate, Res.phpBB_200_ContentType);
			this.LoginPath = "/login.php";
			this.NewTopicPath = "/posting.php?mode=newtopic&f={0}";
			this.PagePath = "/viewforum.php?f={0}&topicdays=0&start={1}";
			this.TopicPath = "/viewtopic.php?t={0}";
			this.RegisterPath = "/profile.php?mode=register";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("ISO-8859-1");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.phpBB_200_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding)
				);
				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				
				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 1)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("_data"))
					{
						string val = HttpUtility.UrlDecode(c.Value);

						if (val.Contains("userid") && !val.Contains("s:6:\"userid\";i:-1")) check++;
					}
				}

				if (check > 0)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.NewTopicPath.Replace("{0}", sectionId.ToString());
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);

				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "sid":
							if (replacements.Keys.Contains("[sid]")) break;
							replacements.Add("[sid]", n.Attributes["value"].Value);
							break;

						case "via":
							if (replacements.Keys.Contains("[via]")) break;
							replacements.Add("[via]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}

				// Check if we got the needed info
				if (!replacements.Keys.Contains("[via]")) replacements.Add("[via]", "null");
				if (replacements.Count != 2) replacements.Clear();

				Http.SessionCookies.Add(this.User.Cookies);

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			topic.Title = HttpUtility.UrlEncode(topic.Title, this.SiteEncoding);
			topic.Content = HttpUtility.UrlEncode(topic.Content, this.SiteEncoding);

			string url = this.BaseUrl + "/posting.php";
			string data = this.Details.Format(topic, this.TemplateReplacements);
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.phpBB_200_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			// Empty read topics cookie
			var cookies = from Cookie c in Http.SessionCookies
						  where c.Name.EndsWith("_t")
						  select c;

			foreach (Cookie c in cookies) c.Value = String.Empty;

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				topic.Title = HttpUtility.UrlEncode(topic.Title, this.SiteEncoding);
				topic.Content = HttpUtility.UrlEncode(topic.Content, this.SiteEncoding);

				url = this.BaseUrl + "/posting.php";
				data = this.Details.Format(topic, this.TemplateReplacements);
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.phpBB_200_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page > 1) ? page - 1 : 0;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page * siteTopicsPerPage);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//img[@alt='Reply with quote']");
				string link = HttpUtility.HtmlDecode(nodes[0].ParentNode.GetAttributeValue("href", String.Empty));
				
				nodes = doc.DocumentNode.SelectNodes("//*[@class='maintitle']");
				string title = HttpUtility.HtmlDecode(nodes[0].InnerText).Trim();

				req = Http.Prepare((link.StartsWith("http:")) ? link : this.BaseUrl + "/" + link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				string content = doc.DocumentNode.SelectNodes("//textarea[@name='message']")[0].InnerText;

				content = HttpUtility.HtmlDecode(content.Substring(content.IndexOf(']') + 1)).Trim();
				content = content.Substring(0, content.Length - "[/quote]".Length);

				// Empty read topics cookie
				var cookies = from Cookie c in Http.SessionCookies
							  where c.Name.EndsWith("_t")
							  select c;

				foreach (Cookie c in cookies) c.Value = String.Empty;

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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;
			bool allowAdd = false;

			try
			{
				doc.LoadHtml(html);

				nodes = doc.DocumentNode.SelectNodes("//table[@class='forumline']");
				var tables = (from n in nodes
							  where (n.InnerHtml.Contains("class=\"topictitle\"") || n.InnerHtml.Contains("class='topictitle'")) &&
									n.InnerHtml.Contains("Replies")
							  select n).ToArray();
				
				if (tables[tables.Length - 1].InnerHtml.Contains("<td class=\"row3\" colspan=\"6\" height=\"21\">"))
				{
					/*foreach (var n in table[table.Length - 1].SelectNodes(".//td[1]"))
					{
						if (n.InnerHtml.Contains("<b>Forum Topics</b>"))
						{
							allowAdd = false;
							break;
						}
					}*/
					
					foreach (var n in tables[tables.Length - 1].SelectNodes(".//tr"))
					{
						if (!allowAdd)
						{
							nodes = n.SelectNodes(".//td[@class='row3']");
							
							if (nodes.Count > 0 &&
								nodes[0].InnerHtml.Contains("Topics") &&
								!nodes[0].InnerHtml.Contains("Sticky") &&
								nodes[0].GetAttributeValue("colspan", "") == "6")
							{
								allowAdd = true;
							}
						}
						else
						{
							nodes = n.SelectNodes(".//a[@class='topictitle']");
							
							if (nodes.Count > 0)
							{
								var anchors = (from a in nodes
											   where !a.ParentNode.InnerHtml.Contains("Announcement:</b>") &&
													 !a.ParentNode.InnerHtml.Contains("Sticky:</b>")
											   select HttpUtility.HtmlDecode(a.GetAttributeValue("href", "")).Trim()).ToArray();
								
								if (anchors.Length > 0 && anchors[0].Length > 0)
								{
									urls.Add((anchors[0].StartsWith("http:")) ? anchors[0] : this.BaseUrl + "/" + anchors[0]);
								}
							}
						}
					}
				}
				else
				{
					nodes = doc.DocumentNode.SelectNodes("//a[@class='topictitle']");
					var anchors = from n in nodes
								  where !n.ParentNode.InnerHtml.Contains("Announcement:</b>") &&
										!n.ParentNode.InnerHtml.Contains("Sticky:</b>")
								  select HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Trim();

					foreach (string a in anchors)
					{
						if (a.Length > 0) urls.Add((a.StartsWith("http:")) ? a : this.BaseUrl + "/" + a);
					}
				}

				

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}
	}

	internal class SiteType_phpBB_300 : SiteType
	{
		public SiteType_phpBB_300()
		{
			this.Name = "phpBB 3.x.x";
			this.BaseUrl = "";
			this.Details = new SiteTypeDetails(Res.phpBB_300_ContentTemplate, Res.phpBB_300_ContentType);
			this.LoginPath = "/ucp.php?mode=login";
			this.NewTopicPath = "/posting.php?mode=post&f={0}";
			this.PagePath = "/viewforum.php?f={0}&start={1}";
			this.TopicPath = "/viewtopic.php?t={0}";
			this.RegisterPath = "/ucp.php?mode=register";
			this.AllowRedirects = false;
			this.UseFriendlyLinks = false;
			this.SiteEncoding = Encoding.GetEncoding("UTF-8");
			this.User = SiteLoginDetails.Empty;
			this.TemplateReplacements = new Dictionary<string, string>();
		}

		public override void LoginUser(string username, string password)
		{
			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + this.LoginPath;
				var details = new SiteLoginDetails(false, username, password);
				var data = String.Format(
					Res.phpBB_300_Login,
					details.GetUrlSafeUsername(this.SiteEncoding),
					details.GetUrlSafePassword(this.SiteEncoding)
				);
				byte[] rawData = this.SiteEncoding.GetBytes(data);
				int check = 0;
				int parse = -1;

				this.LogoutUser();

				HttpWebRequest req = Http.Prepare(url);
				Stream stream;

				req.Method = "POST";
				req.Referer = url;
				req.ContentType = Res.FormContentType;
				req.ContentLength = rawData.Length;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.FastRequest(req), true) : Http.FastRequest(req);

				// Did the request fail?
				if (result.HasError || Http.SessionCookies.Count < 1)
				{
					ErrorLog.LogException(result.Error);
					this.User = details;
					this.OnLogin(this, new LoginEventArgs(details));
					return;
				}

				if (result.HasResponse) this.SiteEncoding = Encoding.GetEncoding(result.Response.CharacterSet);

				// Check if we did login
				foreach (Cookie c in Http.GetDomainCookies(req.RequestUri))
				{
					if (c.Name.EndsWith("_u"))
					{
						string val = HttpUtility.UrlDecode(c.Value);

						if (int.TryParse(val, out parse) && parse > 1) check++;
					}
				}

				if (check > 0)
				{
					details.IsLoggedIn = true;

					foreach (var c in Http.GetDomainCookies(req.RequestUri))
					{
						details.Cookies.Add(c);
					}
				}
				else
				{
					var error = new Exception(String.Format(
						"Login check failed for '{0}'.\r\nCheck count: {1}.",
						this.BaseUrl,
						check
					));

					ErrorLog.LogException(error);
				}

				this.User = details;
				this.OnLogin(this, new LoginEventArgs(details));
				return;
			});
		}

		public override void LogoutUser()
		{
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			Http.RemoveDomainCookies(uri);

			this.User.IsLoggedIn = false;
			this.User.Cookies = new CookieCollection();
		}

		public override void MakeReady(int sectionId)
		{
			if (!this.User.IsLoggedIn) return;

			AsyncHelper.Run(() =>
			{
				string url = this.BaseUrl + String.Format(this.NewTopicPath, sectionId)/* + this.GetSessionId()*/;
				var doc = new HtmlDocument();
				var replacements = new Dictionary<string, string>();

				HttpWebRequest req = Http.Prepare(url);

				req.Method = "GET";

				HttpResult result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);

				// Did the request fail?
				if (result.HasError || result.Data.Trim().Length == 0)
				{
					ErrorLog.LogException(result.Error);
					this.OnReadyChanged(this, new EventArgs());
					return;
				}

				doc.LoadHtml(result.Data);
				
				// Extract required data
				foreach (HtmlNode n in doc.DocumentNode.SelectNodes("//input[@type='hidden']"))
				{
					switch (n.Attributes["name"].Value)
					{
						case "creation_time":
							if (replacements.Keys.Contains("[creation_time]")) break;
							replacements.Add("[creation_time]", n.Attributes["value"].Value);
							break;

						case "form_token":
							if (replacements.Keys.Contains("[form_token]")) break;
							replacements.Add("[form_token]", n.Attributes["value"].Value);
							break;

						default:
							break;
					}
				}
				
				// Check if we got the needed info
				if (replacements.Count != 2) replacements.Clear();

				Http.SessionCookies.Add(this.User.Cookies);

				// Done
				this.TemplateReplacements = replacements;
				this.OnReadyChanged(this, new EventArgs());

				if (!this.IsReady)
				{
					var error = new Exception(String.Format(
						"MakeReady({0}) failed for site '{1}'.\r\nUrl used: {2}.",
						sectionId,
						this.BaseUrl,
						url
					));

					ErrorLog.LogException(error);
				}
			});
		}

		public override HttpWebRequest CreateTopic(SiteTopic topic)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url = this.BaseUrl + String.Format(this.NewTopicPath, topic.SectionId) + this.GetSessionId();
			string data = this.Details.Format(topic, this.TemplateReplacements);
			byte[] rawData = this.SiteEncoding.GetBytes(data);

			HttpWebRequest req = Http.Prepare(url);
			Stream stream;

			req.Method = "POST";
			req.ContentType = Res.phpBB_300_ContentType;
			req.ContentLength = rawData.Length;
			req.Referer = url;

			stream = req.GetRequestStream();
			stream.Write(rawData, 0, rawData.Length);
			stream.Close();

			return req;
		}

		public override HttpWebRequest[] CreateTopics(SiteTopic[] topics)
		{
			if (!this.User.IsLoggedIn || !this.IsReady) return null;

			string url, data;
			byte[] rawData;
			HttpWebRequest req;
			Stream stream;

			var requests = new List<HttpWebRequest>();

			foreach (SiteTopic topic in topics)
			{
				url = this.BaseUrl + String.Format(this.NewTopicPath, topic.SectionId) + this.GetSessionId();
				data = this.Details.Format(topic, this.TemplateReplacements);
				rawData = this.SiteEncoding.GetBytes(data);
				req = Http.Prepare(url);

				req.Method = "POST";
				req.ContentType = Res.phpBB_300_ContentType;
				req.ContentLength = rawData.Length;
				req.Referer = url;

				stream = req.GetRequestStream();
				stream.Write(rawData, 0, rawData.Length);
				stream.Close();

				requests.Add(req);
			}

			return requests.ToArray();
		}

		public override HttpWebRequest GetPage(int sectionId, int page, int siteTopicsPerPage)
		{
			// Fix
			page = (page > 1) ? page - 1 : 0;

			string url = this.BaseUrl + String.Format(this.PagePath, sectionId, page * siteTopicsPerPage);
			HttpWebRequest req = Http.Prepare(url);

			req.Method = "GET";
			req.Referer = url;

			return req;
		}

		public override SiteTopic GetTopic(string url)
		{
			if (!this.User.IsLoggedIn) return null;

			HtmlDocument doc = new HtmlDocument();
			HttpWebRequest req;
			HttpResult result;

			req = Http.Prepare(url);
			req.Method = "GET";
			req.Referer = url;

			try
			{
				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a");
				var links = (from n in nodes
							 where HttpUtility.HtmlDecode(n.GetAttributeValue("href", "")).Contains("posting.php?mode=quote")
							 select HttpUtility.HtmlDecode(n.GetAttributeValue("href", ""))).ToArray();

				string link = links[0].TrimStart("./".ToCharArray());

				req = Http.Prepare((link.StartsWith("http:")) ? link : this.BaseUrl + "/" + link);
				req.Method = "GET";
				req.Referer = url;

				result = this.AllowRedirects ? Http.HandleRedirects(Http.Request(req), false) : Http.Request(req);
				doc.LoadHtml(result.Data);

				ErrorLog.LogException(result.Error);

				string title = doc.DocumentNode.SelectNodes("//input[@name='subject']")[0].GetAttributeValue("value", String.Empty);
				string content = doc.DocumentNode.SelectNodes("//textarea[@name='message']")[0].InnerText;

				title = HttpUtility.HtmlDecode(title);
				title = title.Substring(title.IndexOf(':') + 1).Trim();
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

		public override SiteTopic GetTopic(int topicId)
		{
			string url = this.BaseUrl + String.Format(this.TopicPath, topicId);

			return this.GetTopic(url);
		}

		public override string[] GetTopicUrls(string html)
		{
			if (html == null || html.Trim().Length == 0) return new string[0];

			var urls = new List<string>();
			var doc = new HtmlDocument();
			HtmlNodeCollection nodes;
			HtmlNode p;
			string url;
			bool allowAdd = true;

			try
			{
				doc.LoadHtml(html);

				var links = from link in doc.DocumentNode.SelectNodes("//a")
							where link.GetAttributeValue("class", "").Contains("topictitle")
							select link;

				foreach (var a in links)
				{
					p = a.ParentNode.ParentNode;

					if (p.Name.ToLower() == "tr" || p.Name.ToLower() == "td")
					{
						nodes = p.SelectNodes(".//img");

						foreach (var n in nodes)
						{
							if (n.GetAttributeValue("src", "").Contains("announce") || n.GetAttributeValue("src", "").Contains("sticky"))
							{
								allowAdd = false;
								break;
							}
						}

						if (allowAdd)
						{
							url = HttpUtility.HtmlDecode(a.GetAttributeValue("href", "")).TrimStart("./".ToCharArray());
							urls.Add((url.StartsWith("http:")) ? url : this.BaseUrl + "/" + url);
						}

						allowAdd = true;
					}
					else if (!p.ParentNode.InnerHtml.Contains("announce_") && !p.ParentNode.InnerHtml.Contains("sticky_"))
					{
						url = HttpUtility.HtmlDecode(a.GetAttributeValue("href", "")).TrimStart("./".ToCharArray());
						urls.Add((url.StartsWith("http:")) ? url : this.BaseUrl + "/" + url);
					}
				}

				return urls.ToArray();
			}
			catch (Exception error)
			{
				ErrorLog.LogException(error);
				return new string[0];
			}
		}

		private string GetSessionId()
		{
			if (!this.User.IsLoggedIn) return string.Empty;

			string sid = "&";
			var uri = new Uri(this.BaseUrl, UriKind.Absolute);

			foreach (Cookie c in this.User.Cookies)
			{
				if (c.Name.EndsWith("_sid") && c.Value.Length > 0)
				{
					sid += "sid=" + c.Value;
				}
			}

			if (sid == "&")
			{
				foreach (Cookie c in Http.GetDomainCookies(uri))
				{
					if (c.Name.EndsWith("_sid") && c.Value.Length > 0)
					{
						sid += "sid=" + c.Value;
					}
				}
			}

			return (sid.Length > 1) ? sid : String.Empty;
		}
	}
	#endregion
}
