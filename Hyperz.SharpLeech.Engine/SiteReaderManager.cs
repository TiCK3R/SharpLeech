using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Hyperz.SharpLeech.Engine.Html;
using Hyperz.SharpLeech.Engine.Net;
using Microsoft.CSharp;

using Res = Hyperz.SharpLeech.Engine.Properties.Resources;

namespace Hyperz.SharpLeech.Engine
{
	public sealed class SiteReaderManager
	{
		public static ObservableCollection<SiteReader> SiteReaders { get; private set; }

		static SiteReaderManager()
		{
			SiteReaders = new ObservableCollection<SiteReader>();
		}

		public static bool HasSite(string siteName)
		{
			for (int i = 0; i < SiteReaders.Count; i++)
			{
				if (SiteReaders[i].SiteName == siteName) return true;
			}

			return false;
		}

		public static void Load(string path)
		{
			string[] files = Directory.GetFiles(path, "*" + Res.SiteReaderExt, SearchOption.TopDirectoryOnly);

			SiteReaders = new ObservableCollection<SiteReader>();

			if (!Directory.Exists(Environment.CurrentDirectory + @"\CacheDir"))
			{
				Directory.CreateDirectory(Environment.CurrentDirectory + @"\CacheDir");
			}
			else
			{
				var cacheFiles = Directory.GetFiles(
					Environment.CurrentDirectory + @"\CacheDir",
					"*.dll",
					SearchOption.TopDirectoryOnly
				);

				try
				{
					foreach (var f in cacheFiles) File.Delete(f);
				}
				catch (Exception error)
				{
					ErrorLog.LogException(error);
				}
			}

			foreach (string file in files)
			{
				try
				{
					XmlDocument doc = new XmlDocument();
					SiteReader sr;

					doc.Load(file);

					var nodes = doc.SelectNodes("//Section");
					var root = doc.SelectSingleNode("//SiteReader");
					var sections = new Dictionary<string, int>(nodes.Count);

					foreach (XmlNode n in nodes)
					{
						var key = n.Attributes["title"].InnerText;
						var id = int.Parse(n.Attributes["id"].InnerText);

						/*if (sections.ContainsKey(key))
						{
							sections.Add(key, id);
						}*/

						while (sections.ContainsKey(key)) key += ' ';

						sections.Add(key, id);
					}

					var name = doc.SelectSingleNode("//SiteName").InnerText.Trim();
					var url = doc.SelectSingleNode("//BaseUrl").InnerText.Trim();
					var topicsPp = int.Parse(doc.SelectSingleNode("//TopicsPerPage").InnerText.Trim());
					var type = doc.SelectSingleNode("//Type").InnerText.Trim();
					var encoding = doc.SelectSingleNode("//DefaultEncoding").InnerText.Trim();
					var redirects = bool.Parse(doc.SelectSingleNode("//AllowRedirects").InnerText.Trim());
					var friendlyUrls = bool.Parse(doc.SelectSingleNode("//UseFriendlyLinks").InnerText.Trim());
					var code = doc.SelectSingleNode("//Code");

					if (code != null && code.InnerText.Trim().Length > 0)
					{
						Type compiled = Compile(
							code.InnerText,
							name,
							root.Attributes["pluginVersion"].InnerText,
							root.Attributes["pluginAuthor"].InnerText
						);
						Object[] args = new Object[] {
							name,
							url,
							type,
							topicsPp,
							sections,
							encoding,
							redirects,
							friendlyUrls
						};

						sr = (SiteReader)Activator.CreateInstance(compiled, args);
					}
					else
					{
						sr = new SiteReader(name, url, type, topicsPp, sections, encoding, redirects, friendlyUrls);
					}

					SiteReaders.Add(sr);
				}
				catch (Exception error)
				{
					ErrorLog.LogException(error);
				}
			}
		}

		private static Type Compile(string csharpCode, string siteName, string version = "0.0.0.0", string author = "")
		{
			Type output = null;

			string className;
			long ticks = DateTime.Now.Ticks;
			string name = siteName.Replace("\"", "").Replace("\\", "");
			string header = Res.SiteReaderTemplateHeader;
			string code = Res.SiteReaderTemplate;
			string dllPath = Environment.CurrentDirectory + @"\CacheDir\{0:x}.dll";
			string ns = "Hyperz.SharpLeech.SiteReaders.";
			string allowedNameChars =
				"_0123456789" +
				"abcdefghijklmnopqrstuvwxyz" +
				"abcdefghijklmnopqrstuvwxyz".ToUpper();

			for (int i = 0; i < siteName.Length; i++)
			{
				if (!allowedNameChars.Contains(siteName[i]))
				{
					siteName = siteName.Replace(siteName[i], '_');
				}
			}
			
			author = (author == null || author.Trim().Length == 0) ? "" : author + " ";
			version = (version == null || version.Trim().Length == 0) ? "1.0.*" : version;

			className = String.Format("_SR_{0}_{1:x}", siteName, ticks);
			code = code.Replace("[__CLASS__]", className);
			code = code.Replace("[__CODE__]", csharpCode);
			header = header.Replace("[__NAME__]", name);
			header = header.Replace("[__AUTHOR__]", author);
			header = header.Replace("[__VERSION__]", version);

			using (CSharpCodeProvider codeProvider = new CSharpCodeProvider())
			{
				var param = new CompilerParameters();

				param.CompilerOptions = "/optimize";
				param.GenerateExecutable = false;
				param.GenerateInMemory = false;
				param.IncludeDebugInformation = false;
				param.OutputAssembly = String.Format(dllPath, ticks);
				param.TreatWarningsAsErrors = false;

				param.ReferencedAssemblies.Add("System.dll");
				param.ReferencedAssemblies.Add("System.Core.dll");
				param.ReferencedAssemblies.Add("System.Data.dll");
				param.ReferencedAssemblies.Add("System.Xml.dll");
				param.ReferencedAssemblies.Add("System.Xml.Linq.dll");
				param.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
				param.ReferencedAssemblies.Add("Hyperz.SharpLeech.Engine.dll");
				//param.ReferencedAssemblies.Add("Hyperz.SharpLeech.Engine.Core.dll");

				var result = codeProvider.CompileAssemblyFromSource(param, new string[] { header + code });

				if (result.Errors.HasErrors)
				{
					string err = "Error while compiling '" + className + "'.\r\n\r\n";
					foreach (CompilerError e in result.Errors) err += e.ErrorText + Environment.NewLine;
					
					ErrorLog.LogException(new Exception(err));

					return null;
				}
				
				output = result.CompiledAssembly.GetType(ns + className, true, true);
			}

			return output;
		}
	}
}
