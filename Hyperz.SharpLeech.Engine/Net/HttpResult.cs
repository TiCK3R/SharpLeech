using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Hyperz.SharpLeech.Engine.Net
{
	public class HttpResult
	{
		public string Data { get; private set; }
		public HttpWebResponse Response { get; private set; }
		public Exception Error { get; private set; }
		public CookieCollection Cookies { get; private set; }
		public DateTime Date { get; private set; }

		public string FormattedDate
		{
			get { return String.Format("{0:dd/MM/yyyy} {1:HH:mm:ss}", this.Date, this.Date); }
		}

		public bool HasCookies
		{
			get { return (this.Cookies != null && this.Cookies.Count > 0); }
		}

		public bool HasError
		{
			get { return (this.Error != null); }
		}
		
		public bool HasResponse
		{
			get { return (this.Response != null); }
		}

		public HttpResult(string data, HttpWebResponse response = null, Exception error = null)
		{
			this.Data = (data == null) ? string.Empty : data;
			this.Date = DateTime.Now;
			this.Error = error;
			this.Cookies = (response == null) ? null : response.Cookies;
			this.Response = response;
		}

		public override string ToString()
		{
			return this.Data;
		}
	}
}
