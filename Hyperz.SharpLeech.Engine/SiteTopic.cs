using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Hyperz.SharpLeech.Engine
{
	public class SiteTopic
	{
		public static readonly SiteTopic Empty = new SiteTopic();

		public string Title { get; set; }
		public string Content { get; set; }
		public string Url { get; set; }
		public int SectionId { get; set; }
		public int IconId { get; set; }
		public string Hash
		{
			get { return GetUrlHash(this.Url); }
		}

		public SiteTopic(string title = "", string content = "", int sectionId = 0, int iconId = 0, string url = "")
		{
			this.Title = title;
			this.Content = content;
			this.SectionId = sectionId;
			this.IconId = iconId;
			this.Url = url;
		}

		public static string GetUrlHash(string url)
		{
			if (url != null && url.Length > 0)
			{
				string hash = String.Empty;
				byte[] buffer = Encoding.ASCII.GetBytes(url.Replace("http://www.", ""));

				using (var sha = new SHA1CryptoServiceProvider())
				{
					hash = BitConverter.ToString(sha.ComputeHash(buffer));
					sha.Clear();
				}

				return hash.Replace("-", "").ToLower();
			}
			else
			{
				return null;
			}
		}

		public static string GetUrlHash(SiteTopic topic)
		{
			return GetUrlHash(topic.Url);
		}

		public override string ToString()
		{
			return this.Title;
		}
	}
}
