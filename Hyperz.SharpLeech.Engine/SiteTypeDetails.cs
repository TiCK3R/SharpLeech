using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Hyperz.SharpLeech.Engine.Net;

namespace Hyperz.SharpLeech.Engine
{
	public class SiteTypeDetails
	{
		public string ContentTemplate { get; set; }
		public string ContentType { get; set; }

		public SiteTypeDetails(string contentTemplate, string contentType)
		{
			this.ContentTemplate = contentTemplate;
			this.ContentType = contentType;
		}

		public virtual string Format(SiteTopic topic, Dictionary<string, string> replacements = null)
		{
			string result = this.ContentTemplate;

			if (replacements != null)
			{
				foreach (var replacement in replacements)
				{
					result = result.Replace(replacement.Key, replacement.Value);
				}
			}

			result = String.Format(
				result,
				topic.Title,		// {0}
				topic.Content,		// {1}
				topic.SectionId,	// {2}
				topic.IconId		// {3}
			);

			return result;
		}

		public new virtual string ToString()
		{
			return this.ContentTemplate;
		}
	}
}
