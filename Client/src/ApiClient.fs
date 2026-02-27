module ApiClient

open Fable.Remoting.Client
open Shared

let AuthApi: IAuthApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<IAuthApi>

let TodoApi: ITodoApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<ITodoApi>
