module RemotingUtil

open Fable.Remoting.Giraffe
open Fable.Remoting.Server
open Giraffe
open Microsoft.AspNetCore.Http

let routeBuilder (typeName: string) (methodName: string) = $"/api/%s{typeName}/%s{methodName}"

let handlerFromApi (implementation: HttpContext -> 'api) : HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder routeBuilder
    |> Remoting.fromContext implementation
    |> Remoting.buildHttpHandler
