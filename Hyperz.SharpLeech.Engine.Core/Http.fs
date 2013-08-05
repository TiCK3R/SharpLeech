#light

namespace Hyperz.SharpLeech.Engine.Core

open System
open System.Collections
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Net
open System.Reflection
open System.Text
open Microsoft.FSharp.Control
open Microsoft.FSharp.Control.CommonExtensions
open Microsoft.FSharp.Control.WebExtensions
open Hyperz.SharpLeech.Engine.Net

module Http =
    
    //let mutable AllowRedirect = false
    //let mutable AutomaticDecompression = DecompressionMethods.Deflate ||| DecompressionMethods.GZip
    let mutable KeepAlive = true
    let mutable MaxRedirects = 2
    //let mutable OnlySeoRedirects = true
    let mutable Pipelined = false
    let mutable SessionCookies = new CookieCollection()
    let mutable Timeout = 100000
    let mutable UseCompression = false
    let mutable UserAgent =
        String.Format(
            "Mozilla/5.0 (Windows; N; {0}; 64Bit:{1}; .NET CLR {2}) SLFSCore/{3} SharpLeech/2.x.x",
            Environment.OSVersion,
            Environment.Is64BitProcess,
            Environment.Version,
            Reflection.Assembly.GetExecutingAssembly().GetName().Version
        )


    let RemoveDomainCookies (uri : Uri) =
        lock SessionCookies (fun () ->
            seq { for c in SessionCookies -> c }
            |> Seq.filter (fun c -> not(c.Domain.Contains(uri.Host.Replace("www.", ""))))
            |> fun cookies -> (new CookieCollection(), cookies)
            |> fun (cc, cookies) -> SessionCookies <- cc; cookies
            |> Seq.iter (fun c -> SessionCookies.Add(c))
        )


    let GetDomainCookies (uri : Uri) =
        lock SessionCookies (fun () ->
            seq { for c in SessionCookies -> c }
            |> Seq.filter (fun c -> c.Domain.Contains(uri.Host.Replace("www.", "")))
            //|> fun cookies -> cookies :?> Cookie[]
        )


    let GetCookieContainer () =
        lock SessionCookies (fun () ->
            new CookieContainer()
            |> fun cc -> cc.MaxCookieSize <- 1048576; cc
            |> fun cc -> cc.Add SessionCookies; cc
        )


    let FixCookieDomains () =
        lock SessionCookies (fun () ->
            for c in SessionCookies do
                if c.Domain.StartsWith(".") then
                    c.Domain <- c.Domain.TrimStart(".".ToCharArray())
        )


    let Prepare (url : string) =
        let req = WebRequest.Create(url.Trim()) :?> HttpWebRequest

        req.AllowAutoRedirect <- false
        req.CookieContainer <- GetCookieContainer()
        req.KeepAlive <- KeepAlive
        req.MaximumResponseHeadersLength <- 500
        req.Pipelined <- Pipelined
        req.ReadWriteTimeout <- Timeout
        req.Timeout <- Timeout
        req.UserAgent <- UserAgent
        
        //req.Headers.Add("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7")

        req


    let Request (req : HttpWebRequest) =
        try
            if UseCompression then req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate")

            let rsp = req.GetResponse() :?> HttpWebResponse
            let enc = rsp.ContentEncoding.ToLower()

            use s =
                if enc.Contains("gzip") then
                    new GZipStream(
                        rsp.GetResponseStream(),
                        CompressionMode.Decompress
                    ) :> Stream
                elif enc.Contains("deflate") then
                    new DeflateStream(
                        rsp.GetResponseStream(),
                        CompressionMode.Decompress
                    ) :> Stream
                else
                    rsp.GetResponseStream()

            lock SessionCookies (fun () ->
                if not(rsp.Cookies = null) && (rsp.Cookies.Count > 0) then
                    SessionCookies.Add(rsp.Cookies)
                    //FixCookieDomains()
            )
            
            use sr = new StreamReader(s, Encoding.GetEncoding(rsp.CharacterSet))
            new HttpResult(sr.ReadToEnd(), rsp, null)
        with _ as error ->
            new HttpResult(String.Empty, null, error)


    let FastRequest (req : HttpWebRequest) =
        try
            if UseCompression then req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate")

            let rsp = req.GetResponse() :?> HttpWebResponse
            let enc = rsp.ContentEncoding.ToLower()

            lock SessionCookies (fun () ->
                if not(rsp.Cookies = null) && (rsp.Cookies.Count > 0) then
                    SessionCookies.Add(rsp.Cookies)
                    //FixCookieDomains()
            )

            rsp.Close()

            new HttpResult(String.Empty, rsp, null)
        with _ as error ->
            new HttpResult(String.Empty, null, error)
    
    
    let AsyncRequest (req : HttpWebRequest) = async {
        try
            if UseCompression then req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate")

            let! rsp' = req.AsyncGetResponse()
            let rsp = rsp' :?> HttpWebResponse
            let enc = rsp.ContentEncoding.ToLower()

            use s =
                if enc.Contains("gzip") then
                    new GZipStream(
                        rsp.GetResponseStream(),
                        CompressionMode.Decompress
                    ) :> Stream
                elif enc.Contains("deflate") then
                    new DeflateStream(
                        rsp.GetResponseStream(),
                        CompressionMode.Decompress
                    ) :> Stream
                else
                    rsp.GetResponseStream()

            lock SessionCookies (fun () ->
                if not(rsp.Cookies = null) && (rsp.Cookies.Count > 0) then
                    SessionCookies.Add(rsp.Cookies)
                    FixCookieDomains()
            )

            use sr = new StreamReader(s, Encoding.GetEncoding(rsp.CharacterSet))
            return new HttpResult(sr.ReadToEnd(), rsp, null)
        with _ as error ->
            return new HttpResult(String.Empty, null, error)}
    
    
    let AsyncFastRequest (req : HttpWebRequest) = async {
        try
            if UseCompression then req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate")

            let! rsp' = req.AsyncGetResponse()
            let rsp = rsp' :?> HttpWebResponse
            
            lock SessionCookies (fun () ->
                if not(rsp.Cookies = null) && (rsp.Cookies.Count > 0) then
                    SessionCookies.Add(rsp.Cookies)
                    FixCookieDomains()
            )
            
            rsp.Close()

            return new HttpResult(String.Empty, rsp, null)
        with _ as error ->
            return new HttpResult(String.Empty, null, error)}


    let HandleRedirects (result : HttpResult, isFastRequest : bool) =
        let mutable count = 0
        //let mutable req : HttpWebRequest = null
        let mutable r = result
        let mutable url = String.Empty
        let mutable status = if result.HasError then HttpStatusCode.NotFound
                             else result.Response.StatusCode

        let allowContinue statusCode =
            match statusCode with
            | HttpStatusCode.MovedPermanently -> true
            | HttpStatusCode.Found -> true
            | _ -> false

        let needsRedirect (location : string) =
            let uri = ref (new Uri("http://google.com"))
            (not(location = null) && location.Length > 0 && Uri.TryCreate(location, UriKind.Absolute, uri))

        if r.HasError || MaxRedirects < 1 then
            result
        elif MaxRedirects = 1 then
            url <- r.Response.Headers.["Location"]

            if needsRedirect url then
                if isFastRequest then FastRequest(Prepare(url))
                else Request(Prepare(url))
            else
                result
        else
            while count < MaxRedirects && not r.HasError && allowContinue r.Response.StatusCode do
                url <- r.Response.Headers.["Location"]
                if needsRedirect url then
                    r <- if isFastRequest then FastRequest(Prepare(url))
                         else Request(Prepare(url))
                count <- count + 1
            
            r