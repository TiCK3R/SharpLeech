using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Hyperz.SharpLeech.Engine.Net;

namespace Hyperz.SharpLeech.Engine
{
	public class SiteLoginDetails
	{
		public static readonly SiteLoginDetails Empty = new SiteLoginDetails(false);

		public string Username { get; set; }
		public string Password { get; set; }
		public bool IsLoggedIn { get; set; }
		public CookieCollection Cookies { get; set; }

		public SiteLoginDetails(bool isLoggedIn, string username = "", string password = "", CookieCollection cookies = null)
		{
			this.IsLoggedIn = isLoggedIn;
			this.Username = username;
			this.Password = password;
			this.Cookies = (cookies == null) ? new CookieCollection() : cookies;
		}

		public string GetUrlSafeUsername(Encoding encoding = null)
		{
			return (encoding == null) ? HttpUtility.UrlEncode(this.Username, Encoding.GetEncoding("ISO-8859-1")) : HttpUtility.UrlEncode(this.Username, encoding);
		}

		public string GetUrlSafePassword(Encoding encoding = null)
		{
			return (encoding == null) ? HttpUtility.UrlEncode(this.Password, Encoding.GetEncoding("ISO-8859-1")) : HttpUtility.UrlEncode(this.Password, encoding);
		}

		public override string ToString()
		{
			return this.Username;
		}
	}
}
