using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Hyperz.SharpLeech.Engine.Html;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.Engine.Net
{
	public sealed class Http
	{
		//public static Boolean AllowRedirect = false;
		//public static DecompressionMethods AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
		public static Boolean KeepAlive = true;
		public static Int32 MaxRedirects = 2;
		//public static Boolean OnlySeoRedirects = true;
		public static Boolean Pipelined = false;
		//public static IWebProxy Proxy = GlobalProxySelection.GetEmptyWebProxy();
		public static IWebProxy Proxy = HttpWebRequest.GetSystemWebProxy();
		public static CookieCollection SessionCookies = new CookieCollection();
		public static Int32 Timeout = 30000;
		public static Boolean UseCompression = true;
		public static String UserAgent = String.Format(
			Res.UserAgentFormat,
			Environment.OSVersion,
			Environment.Is64BitProcess,
			Environment.Version,
			Assembly.GetExecutingAssembly().GetName().Version
		);

		public static void RemoveDomainCookies(Uri uri)
		{
			lock (SessionCookies)
			{
				string host = uri.Host.Replace("www.", "").TrimStart('.');
				var sc = from Cookie c in SessionCookies
						 where !c.Domain.Contains(host)
						 select c;

				SessionCookies = new CookieCollection();
				foreach (Cookie c in sc) SessionCookies.Add(c);
			}
		}

		public static IEnumerable<Cookie> GetDomainCookies(Uri uri)
		{
			string host = uri.Host.Replace("www.", "").TrimStart('.');
			var cookies = from Cookie c in SessionCookies
						  where c.Domain.Contains(host)
						  select c;

			return cookies;
		}

		public static CookieContainer GetCookieContainer()
		{
			var cc = new CookieContainer() { MaxCookieSize = 1048576 };
			lock (SessionCookies) cc.Add(SessionCookies);
			return cc;
		}

		public static void FixCookieDomains()
		{
			/*lock SessionCookies (fun () ->
            for c in SessionCookies do
                if c.Domain.StartsWith(".") then
                    c.Domain <- c.Domain.TrimStart(".".ToCharArray())
        	)*/
		}

		public static HttpWebRequest Prepare(string url, string method = "GET", string contentType = null)
		{
			var req = (HttpWebRequest)WebRequest.Create(url.Trim());

			req.AllowAutoRedirect = false;
			req.ContentType = contentType;
			req.CookieContainer = GetCookieContainer();
			req.KeepAlive = KeepAlive;
			req.MaximumResponseHeadersLength = 4096;
			req.Method = method;
			req.Pipelined = Pipelined;
			req.Proxy = Proxy;
			req.ReadWriteTimeout = Timeout;
			req.Timeout = Timeout;
			req.UserAgent = UserAgent;
			
			//req.Headers.Add("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7");

			return req;
		}

		public static HttpResult Request(HttpWebRequest req)
		{
			HttpWebResponse rsp = null;
			HttpResult output = null;
			String enc, result = String.Empty;

			try
			{
				if (UseCompression) req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

				rsp = (HttpWebResponse)req.GetResponse();
				enc = rsp.ContentEncoding.ToLower();

				if (enc.Contains("gzip"))
				{
					using (var s = new GZipStream(rsp.GetResponseStream(), CompressionMode.Decompress))
					using (var sr = new StreamReader(s, Encoding.GetEncoding(rsp.CharacterSet)))
					{
						result = sr.ReadToEnd();
						s.Close();
						sr.Close();
					}
				}
				else if (enc.Contains("deflate"))
				{
					using (var s = new DeflateStream(rsp.GetResponseStream(), CompressionMode.Decompress))
					using (var sr = new StreamReader(s, Encoding.GetEncoding(rsp.CharacterSet)))
					{
						result = sr.ReadToEnd();
						s.Close();
						sr.Close();
					}
				}
				else
				{
					using (var s = rsp.GetResponseStream())
					using (var sr = new StreamReader(s, Encoding.GetEncoding(rsp.CharacterSet)))
					{
						result = sr.ReadToEnd();
						s.Close();
						sr.Close();
					}
				}

				lock (SessionCookies)
				{
					if (rsp.Cookies != null && rsp.Cookies.Count > 0) SessionCookies.Add(rsp.Cookies);
					//FixCookieDomains()
				}

				output = new HttpResult(result, rsp, null);
				rsp.Close();
			}
			catch (Exception error)
			{
				output = new HttpResult(String.Empty, null, error);
			}

			return output;
		}

		public static HttpResult FastRequest(HttpWebRequest req)
		{
			HttpWebResponse rsp = null;
			HttpResult output = null;
			String enc, result = String.Empty;

			try
			{
				req.KeepAlive = false;

				if (UseCompression) req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

				rsp = (HttpWebResponse)req.GetResponse();
				enc = rsp.ContentEncoding.ToLower();

				lock (SessionCookies)
				{
					if (rsp.Cookies != null && rsp.Cookies.Count > 0) SessionCookies.Add(rsp.Cookies);
				}

				output = new HttpResult(result, rsp, null);
				rsp.Close();
			}
			catch (Exception error)
			{
				output = new HttpResult(String.Empty, null, error);
			}

			return output;
		}

		public static HttpResult HandleRedirects(HttpResult result, bool isFastRequest)
		{
			var count = 0;
			var r = result;
			var url = String.Empty;
			var status = (result.HasError) ? HttpStatusCode.NotFound : result.Response.StatusCode;

			Func<HttpStatusCode, Boolean> allowContinue = statusCode =>
			{
				switch (statusCode)
				{
					case HttpStatusCode.MovedPermanently:
					case HttpStatusCode.Found:
						return true;
					default:
						return false;
				}
			};

			Func<String, Boolean> needsRedirect = location =>
			{
				Uri uri = null;
				return (location != null && location.Length > 0 && Uri.TryCreate(location, UriKind.Absolute, out uri));
			};

			if (r.HasError || MaxRedirects < 1) return result;
			else if (MaxRedirects == 1)
			{
				url = r.Response.Headers["Location"];

				if (needsRedirect(url))
				{
					return (isFastRequest) ? FastRequest(Prepare(url)) : Request(Prepare(url));
				}
				else return result;
			}
			else
			{
				while (count < MaxRedirects && !r.HasError && allowContinue(r.Response.StatusCode))
				{
					url = r.Response.Headers["Location"];

					if (needsRedirect(url))
					{
						r = (isFastRequest) ? FastRequest(Prepare(url)) : Request(Prepare(url));
					}

					count++;
				}

				return r;
			}
		}

		public static HttpResult Get(string url)
		{
			return HandleRedirects(Request(Prepare(url)), false);
		}

		public static HtmlDocument GetDoc(string url)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(Get(url).Data);
			return doc;
		}
	}
}
