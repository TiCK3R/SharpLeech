using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.Engine.Radio
{
	public class RadioStation
	{
		public string Name { get; set; }
		public string Source { get; set; }
		public RadioStreamType StreamType { get; set; }

		public RadioStation(string name = "", string source = "", RadioStreamType streamType = RadioStreamType.Unknown)
		{
			this.Name = name;
			this.Source = source;
			this.StreamType = streamType;
		}
	}

	public enum RadioStreamType
	{
		Audio,
		Video,
		Unknown
	}
}
