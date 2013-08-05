namespace Hyperz.SharpLeech.Engine.Core

open System
open System.IO
open System.Net
open Microsoft.FSharp.Control
open Microsoft.FSharp.Control.CommonExtensions
open Microsoft.FSharp.Control.WebExtensions
open Hyperz.SharpLeech.Engine.Net

module AsyncHelper =
    
    let RunAction (func : Action) =
        async { return func.Invoke() }
        |> Async.Start


    let RunParallel (computations : Async<'T>[]) =
        computations
        |> Async.Parallel
        |> Async.RunSynchronously