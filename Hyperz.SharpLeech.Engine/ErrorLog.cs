using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.Engine
{
	public sealed class ErrorLog
	{
		public static String LogFile { get; set; }
		private static XDocument log = new XDocument();
		private static XElement root = null;

		static ErrorLog()
		{
			LogFile = Environment.CurrentDirectory + @"\Errors.log";

			if (!File.Exists(LogFile))
			{
				try { File.WriteAllText(LogFile, Res.ErrorLogTemplate); }
				catch { }
			}

			try
			{
				log = XDocument.Parse(File.ReadAllText(LogFile));
				var elms = from d in log.Descendants()
						   where d.Name == "Errors"
						   select d;

				if (elms == null || elms.ToArray().Length < 1)
				{
					log = XDocument.Parse(Res.ErrorLogTemplate);
					elms = from d in log.Descendants()
						   where d.Name == "Errors"
						   select d;
					root = elms.ToArray()[0];
				}
				else
				{
					root = elms.ToArray()[0];
				}
			}
			catch
			{
				log = XDocument.Parse(Res.ErrorLogTemplate);
				var elms = from d in log.Descendants()
						   where d.Name == "Errors"
						   select d;
				root = elms.ToArray()[0];
			}
		}

		public static void LogException(Exception ex)
		{
			if (ex == null || ex.GetType() == typeof(System.Threading.ThreadAbortException)) return;

			root.Add(SerializeException(ex));
			lock (log) log.Save(LogFile);
			//log.Save(LogFile);
		}

		public static XElement SerializeException(Exception ex)
		{
			String n = "NULL";
			DateTime now = DateTime.Now;
			String logDate = String.Format("{0:dd/MM/yyyy} at {1:HH:mm:ss}", now, now);
			XElement r = new XElement(ex.GetType().ToString());

			r.Add(new XElement("LogDate", logDate));
			r.Add(new XElement("Message", (ex.Message != null) ? ex.Message.Trim() : n));
			r.Add(new XElement("Source", (ex.Source != null) ? ex.Source : n));
			r.Add(new XElement("TargetSite", (ex.TargetSite != null) ? ex.TargetSite.Name : n));
			r.Add(new XElement("HelpLink", (ex.HelpLink != null) ? ex.HelpLink : n));
			r.Add(new XElement("StackTrace", (ex.StackTrace != null) ? ex.StackTrace.Trim() : n));

			if (ex.Data.Count > 0)
			{
				r.Add(new XElement(
					"Data",
					from entry in ex.Data.Cast<DictionaryEntry>()
					let key = entry.Key.ToString()
					let value = (entry.Value == null) ? n : entry.Value.ToString()
					select new XElement(key, value)
				));
			}
			else
			{
				r.Add(new XElement("Data", n));
			}

			if (ex.InnerException != null)
			{
				r.Add(new XElement("InnerException", SerializeException(ex.InnerException)));
			}
			else
			{
				r.Add(new XElement("InnerException", n));
			}

			return r;
		}
	}
}
