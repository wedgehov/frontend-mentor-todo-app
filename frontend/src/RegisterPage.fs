module RegisterPage

open Elmish
open Feliz
open Shared

// Model
type Model =
    { Email: string
      Password: string
      ConfirmPassword: string
      IsLoading: bool
      Error: string option }

let init () : Model =
    { Email = ""
      Password = ""
      ConfirmPassword = ""
      IsLoading = false
      Error = None }

// Msg
type Msg =
    | SetEmail of string
    | SetPassword of string
    | SetConfirmPassword of string
    | AttemptRegister
    | RegisterResult of Result<User, exn>

// Update
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | SetEmail email -> { model with Email = email }, Cmd.none
    | SetPassword pass -> { model with Password = pass }, Cmd.none
    | SetConfirmPassword pass -> { model with ConfirmPassword = pass }, Cmd.none
    | AttemptRegister ->
        if model.Password <> model.ConfirmPassword then
            { model with Error = Some "Passwords do not match" }, Cmd.none
        else
            { model with IsLoading = true; Error = None },
            Auth.register { email = model.Email; password = model.Password } RegisterResult
    | RegisterResult (Ok user) ->
        // This message should be bubbled up to the main update function
        // to navigate to the todos page and store the user.
        { model with IsLoading = false }, Cmd.none
    | RegisterResult (Error ex) ->
        { model with IsLoading = false; Error = Some ex.Message }, Cmd.none

// View
let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [ 
        prop.className "w-full max-w-xs mx-auto"
        prop.children [
            Html.form [ 
                prop.className "bg-white shadow-md rounded px-8 pt-6 pb-8 mb-4"
                prop.onSubmit (fun ev -> ev.preventDefault(); dispatch AttemptRegister)
                prop.children [
                    Html.div [ 
                        prop.className "mb-4"
                        prop.children [
                            Html.label [ 
                                prop.className "block text-gray-700 text-sm font-bold mb-2"
                                prop.htmlFor "email"
                                prop.text "Email Address"
                            ]
                            Html.input [ 
                                prop.className "shadow appearance-none border rounded w-full py-2 px-3 text-gray-700 leading-tight focus:outline-none focus:shadow-outline"
                                prop.id "email"
                                prop.type' "email"
                                prop.placeholder "Email"
                                prop.value model.Email
                                prop.onChange (SetEmail >> dispatch)
                            ]
                        ]
                    ]
                    Html.div [ 
                        prop.className "mb-4"
                        prop.children [
                            Html.label [ 
                                prop.className "block text-gray-700 text-sm font-bold mb-2"
                                prop.htmlFor "password"
                                prop.text "Create Password"
                            ]
                            Html.input [ 
                                prop.className "shadow appearance-none border rounded w-full py-2 px-3 text-gray-700 leading-tight focus:outline-none focus:shadow-outline"
                                prop.id "password"
                                prop.type' "password"
                                prop.placeholder "******************"
                                prop.value model.Password
                                prop.onChange (SetPassword >> dispatch)
                            ]
                        ]
                    ]
                    Html.div [ 
                        prop.className "mb-6"
                        prop.children [
                            Html.label [ 
                                prop.className "block text-gray-700 text-sm font-bold mb-2"
                                prop.htmlFor "confirm-password"
                                prop.text "Confirm Password"
                            ]
                            Html.input [ 
                                prop.className "shadow appearance-none border rounded w-full py-2 px-3 text-gray-700 mb-3 leading-tight focus:outline-none focus:shadow-outline"
                                prop.id "confirm-password"
                                prop.type' "password"
                                prop.placeholder "******************"
                                prop.value model.ConfirmPassword
                                prop.onChange (SetConfirmPassword >> dispatch)
                            ]
                            match model.Error with
                            | Some error -> Html.p [ prop.className "text-red-500 text-xs italic"; prop.text error ]
                            | None -> Html.none
                        ]
                    ]
                    Html.div [ 
                        prop.className "flex items-center justify-between"
                        prop.children [
                            Html.button [ 
                                prop.className "bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded focus:outline-none focus:shadow-outline"
                                prop.type' "submit"
                                prop.disabled model.IsLoading
                                prop.text (if model.IsLoading then "Creating Account..." else "Create New Account")
                            ]
                            Html.a [ 
                                prop.className "inline-block align-baseline font-bold text-sm text-blue-500 hover:text-blue-800"
                                prop.href "#/login"
                                prop.text "Already have an account? Login"
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
