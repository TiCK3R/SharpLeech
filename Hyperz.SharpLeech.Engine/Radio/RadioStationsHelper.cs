using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Hyperz.SharpLeech.Engine.Radio
{
	public class RadioStationsHelper
	{
		public bool IsParsed { get; private set; }
		public RadioStation[] RawRadioStations { get; private set; }
		public StackPanel[] WpfRadioStations { get; private set; }

		private string[] audioTags = { "a", "audio" };
		private string[] videoTags = { "v", "video" };
		private string commentTag = "//";
		private byte maxParts = 3;

		public RadioStationsHelper(string file, Image audioImg = null, Image videoImg = null)
		{
			string data;

			try
			{
				data = File.ReadAllText(file).Replace("\r", "").Replace("\t", "").Trim();
			}
			catch (Exception)
			{
				this.RawRadioStations = new RadioStation[0];
				this.WpfRadioStations = new StackPanel[0];
				this.IsParsed = false;
				return;
			}

			this.RawRadioStations = this.Parse(data);
			this.WpfRadioStations = this.ParseWpf(this.RawRadioStations, audioImg, videoImg);
			this.IsParsed = true;
		}

		private RadioStation[] Parse(string data)
		{
			List<RadioStation> list = new List<RadioStation>();
			string[] split = data.Split(new string[] { "\n" }, int.MaxValue, StringSplitOptions.RemoveEmptyEntries);
			string[] parts;
			bool check;
			int i;

			for (i = 0; i < split.Length; i++)
			{
				if (split[i].Trim().Length > 0 && !split[i].Trim().StartsWith(this.commentTag))
				{
					parts = split[i].Trim().Split(
						";".ToCharArray(),
						this.maxParts,
						StringSplitOptions.RemoveEmptyEntries
					);

					check = (
						parts.Length >= 3 &&
						parts[0].Trim().Length > 0 &&
						parts[1].Trim().Length > 0 &&
						parts[2].Trim().Length > 0
					);

					if (check)
					{
						if (this.audioTags.Contains(parts[0].Trim().ToLower()))
						{
							list.Add(new RadioStation(
								parts[1].Trim(),
								parts[2].Trim(),
								RadioStreamType.Audio
							));
						}
						else if (this.videoTags.Contains(parts[0].Trim().ToLower()))
						{
							list.Add(new RadioStation(
								parts[1].Trim(),
								parts[2].Trim(),
								RadioStreamType.Video
							));
						}
						else
						{
							list.Add(new RadioStation(
								parts[1].Trim(),
								parts[2].Trim(),
								RadioStreamType.Unknown
							));
						}
					}
				}
			}

			return list.ToArray();
		}

		private StackPanel[] ParseWpf(RadioStation[] rawRadioStations, Image audioImg, Image videoImg)
		{
			List<StackPanel> items = new List<StackPanel>();
			StackPanel stack;
			TextBlock txt;
			Image img;
			
			if (audioImg == null) audioImg = new Image();
			if (videoImg == null) videoImg = new Image();

			foreach (var rs in rawRadioStations)
			{
				stack = new StackPanel();
				txt = new TextBlock();
				img = new Image();

				switch (rs.StreamType)
				{
					case RadioStreamType.Audio:
						img.Source = audioImg.Source;
						break;
					case RadioStreamType.Video:
						img.Source = videoImg.Source;
						break;
					case RadioStreamType.Unknown:
					default: break;
				}

				img.Width = img.Height = 16d;
				img.VerticalAlignment = VerticalAlignment.Center;
				txt.Text = rs.Name;
				txt.Margin = new Thickness(3d, 0d, 0d, 0d);
				txt.VerticalAlignment = VerticalAlignment.Center;
				stack.Orientation = Orientation.Horizontal;
				stack.Children.Add(img);
				stack.Children.Add(txt);

				items.Add(stack);
			}

			return items.ToArray();
		}
	}
}
