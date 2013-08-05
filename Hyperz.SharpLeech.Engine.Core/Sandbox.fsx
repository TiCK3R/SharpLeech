#light
System.Environment.CurrentDirectory <- @"C:\Users\Hyperz\Documents\Visual Studio 2010\Projects\Hyperz.SharpLeech\Hyperz.SharpLeech\bin\Debug"
#I @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0"
#r "WindowsBase.dll"
#r "WindowsFormsIntegration.dll"
#r "PresentationCore.dll"
#r "PresentationFramework.dll"
#r "Microsoft.CSharp.dll"
#r "System.dll"
#r "System.Core.dll"
#r "System.Data.dll"
#r "System.Data.DataSetExtensions"
#r "System.Drawing"
#r "System.Xaml.dll"
#r "System.Windows.Forms.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"
#r @"C:\Users\Hyperz\Documents\Visual Studio 2010\Projects\Hyperz.SharpLeech\Hyperz.SharpLeech\bin\release\Interop.WMPLib.dll"
#r @"C:\Users\Hyperz\Documents\Visual Studio 2010\Projects\Hyperz.SharpLeech\Hyperz.SharpLeech\bin\release\AxInterop.WMPLib.dll"
#r @"C:\Users\Hyperz\Documents\Visual Studio 2010\Projects\Hyperz.SharpLeech\Hyperz.SharpLeech\bin\release\Hyperz.SharpLeech.Engine.dll"
#r @"C:\Users\Hyperz\Documents\Visual Studio 2010\Projects\Hyperz.SharpLeech\Hyperz.SharpLeech\bin\release\SharpLeech.exe"
#load "HttpResult.fs"
#load "Http.fs"
#load "AsyncHelper.fs"
open System
open System.Net
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Threading
open System.Text
(*open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Documents
open System.Windows.Input
open System.Windows.Interop
open System.Windows.Media
open System.Windows.Media.Imaging
open System.Windows.Navigation
open System.Windows.Shapes
open System.Windows.Threading*)
open Microsoft.Win32
open AxWMPLib
open Microsoft.FSharp.Control
open Microsoft.FSharp.Control.CommonExtensions
open Microsoft.FSharp.Control.LazyExtensions
open Microsoft.FSharp.Control.WebExtensions
open Hyperz.SharpLeech
open Hyperz.SharpLeech.BBCode
open Hyperz.SharpLeech.Controls
open Hyperz.SharpLeech.Engine
//open Hyperz.SharpLeech.Engine.Core
open Hyperz.SharpLeech.Engine.Html
open Hyperz.SharpLeech.Engine.Irc
open Hyperz.SharpLeech.Engine.Net
open Hyperz.SharpLeech.Engine.Radio
open Hyperz.SharpLeech.Engine.Win32
ServicePointManager.Expect100Continue <- false
ServicePointManager.DefaultConnectionLimit <- 5
Http.Timeout <- 10000000
let output data = File.WriteAllText(@"C:\Users\Hyperz\Desktop\test.html", data)
// ========================================================================================


let wj = DefaultSiteTypes.ByName("vBulletin 3.x.x").CreateInstance();
let html = new HtmlDocument()
let eData = "securitytoken={0}&do=updatepost&ajax=1&postid={1}&wysiwyg=0&message={2}&reason=Byebye&postcount=1"
let msg = "*Removed/Moved to a place worthy of my posts*"

html.LoadHtml(File.ReadAllText(@"C:\Users\Hyperz\Desktop\search.php.htm"))

//wj.BaseUrl <- "http://www.wjunction.com"
wj.Login.AddHandler (fun _ _ -> stdout.WriteLine "Logged in")
wj.LoginUser ("Hyperz", "")

html.DocumentNode.SelectNodes("//a")
|> Seq.filter (fun elm -> not (elm.Id = null))
|> Seq.filter (fun elm -> elm.Id.StartsWith("thread_title_"))
|> Seq.filter (fun elm -> elm.GetAttributeValue("href", "").StartsWith("showthread"))
|> Seq.map (fun elm -> "http://www.wjunction.com/" + elm.GetAttributeValue("href", ""))
|> fun urls ->
    for url in urls do
        let doc = new HtmlDocument()
        let result = Http.Request(Http.Prepare(url))

        if result.Data.Length > 0 then
            doc.LoadHtml(result.Data)
            let postId =
                doc.DocumentNode.SelectNodes("//div")
                |> Seq.filter (fun elm -> not (elm.Id = null))
                |> Seq.filter (fun elm -> elm.Id.StartsWith("post_message_"))
                |> Array.ofSeq
                |> fun a -> a.[0].Id.Replace("post_message_", "")
            let token = doc.DocumentNode.SelectSingleNode("//input[@name='securitytoken']").GetAttributeValue("value", "")
            stdout.WriteLine ("Editing: " + postId + " - token: " + token)
            let req = Http.Prepare("http://www.wjunction.com/editpost.php?do=updatepost&postid=undefined", "POST", "application/x-www-form-urlencoded; charset=UTF-8")
            let postData = Encoding.UTF8.GetBytes(String.Format(eData, token, postId, HttpUtility.UrlEncode(msg, Encoding.UTF8)))
            req.ContentLength <- int64 postData.Length
            use stream = req.GetRequestStream()
            stream.Write(postData, 0, postData.Length)
            stream.Close()
            stdout.WriteLine (not(Http.Request(req).HasError))
        else
            stdout.WriteLine("Failed: " + url)
