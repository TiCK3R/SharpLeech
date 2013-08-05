using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace Hyperz.SharpLeech.BBCode
{
    public class Parser : UserControl
    {
        public static readonly DependencyProperty BBCodeProperty = DependencyProperty.Register(
            "BBCode", typeof(String), typeof(Parser), new PropertyMetadata(OnBBPropertyChanged));
        public string BBCode
        {
            get { return (String)this.GetValue(BBCodeProperty); }
            set { this.SetValue(BBCodeProperty, value); }
        }

        public static readonly DependencyProperty RootElementNameProperty = DependencyProperty.Register(
            "RootElementName", typeof(String), typeof(Parser), new PropertyMetadata(OnBBPropertyChanged));
        public string RootElementName
        {
            get { return (String)this.GetValue(RootElementNameProperty); }
            set { this.SetValue(RootElementNameProperty, value); }
        }

        public static readonly DependencyProperty DefaultElementNameProperty = DependencyProperty.Register(
            "DefaultElementName", typeof(String), typeof(Parser), new PropertyMetadata(OnBBPropertyChanged));
        public string DefaultElementName
        {
            get { return (String)this.GetValue(DefaultElementNameProperty); }
            set { this.SetValue(DefaultElementNameProperty, value); }
        }


        public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(
          "Tags", typeof(TagCollection), typeof(Parser), new PropertyMetadata(OnBBPropertyChanged));
        public TagCollection Tags
        {
            get { return this.GetValue(TagsProperty) as TagCollection; }
            set { this.SetValue(TagsProperty, value); }
        }

        public Parser()
        {
            Tags = new TagCollection();
        }

        private static void OnBBPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (obj is Parser)
            {
                var parser = obj as Parser;
                parser.Parse();
            }
        }

        private void Parse()
        {
            if (String.IsNullOrEmpty(BBCode)) return;
            if (String.IsNullOrEmpty(RootElementName)) return;
            if (String.IsNullOrEmpty(DefaultElementName)) return;

            Debug.Print("PARSING: {0}", BBCode);
            string xaml = BBCode;
            string xamlFormat = "<" + RootElementName + ">{0}</" + RootElementName + ">";
            foreach (var tag in Tags)
                xaml = tag.DoFormating(xaml);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(string.Format(xamlFormat, xaml));
            xaml = "";
            foreach (XmlNode i in doc.FirstChild)
            {
                if (i.NodeType == XmlNodeType.Text)
                    xaml += "<" + DefaultElementName + ">" + i.OuterXml + "</" + DefaultElementName + "> ";
                else
                    xaml += i.OuterXml;
            }

            xaml = string.Format(xamlFormat, xaml);

            System.Windows.Markup.ParserContext pc = new ParserContext();
            pc.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            pc.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            XamlReader rdr = new XamlReader();
            this.Content = XamlReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xaml)), pc);
        }
    }
}
