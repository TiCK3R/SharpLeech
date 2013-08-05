namespace Hyperz.SharpLeech.Engine.Net

open System
open System.IO
open System.Net
open Microsoft.FSharp.Control
open Microsoft.FSharp.Control.CommonExtensions
open Microsoft.FSharp.Control.WebExtensions

type HttpResult (data, response:HttpWebResponse, error:Exception) =

    let mutable data = if (data = null) then String.Empty else data
    let mutable response = response
    let mutable error = error
    let mutable date = DateTime.Now
    let mutable disposed = false
    
    member this.Data
        with get() = data
    member this.Response
        with get() = response
    member this.Error
        with get() = error
    member this.Cookies
        with get() = response.Cookies
    member this.Date
        with get() = date

    member this.FormattedDate
        with get() = date.ToShortDateString() + " " + date.ToLongTimeString()
    member this.HasCookies
        with get() = (response.Cookies.Count > 0)
    member this.HasError
        with get() = not (error = null)
    member this.HasResponse
        with get() = not (response = null)

    override this.ToString() =
        data

    member private this.Dispose(disposing) =
        if not disposed then
            if disposing then
                response.Close()
            disposed <- true

    member this.Dispose() = (this :> IDisposable).Dispose()
    
    interface IDisposable with

        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)