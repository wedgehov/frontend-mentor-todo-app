module RegisterPage

open Elmish
open Feliz
open Shared

// Model
type Model = {
  Email: string
  Password: string
  ConfirmPassword: string
  IsLoading: bool
  Error: string option
}

let init () : Model = {
  Email = ""
  Password = ""
  ConfirmPassword = ""
  IsLoading = false
  Error = None
}

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
  | SetEmail email -> {model with Email = email}, Cmd.none
  | SetPassword pass -> {model with Password = pass}, Cmd.none
  | SetConfirmPassword pass -> {model with ConfirmPassword = pass}, Cmd.none
  | AttemptRegister ->
    if model.Password <> model.ConfirmPassword then
      {model with Error = Some "Passwords do not match"}, Cmd.none
    else
      {model with IsLoading = true; Error = None},
      Auth.register {email = model.Email; password = model.Password} RegisterResult
  | RegisterResult (Ok user) ->
    // This message should be bubbled up to the main update function
    // to navigate to the todos page and store the user.
    {model with IsLoading = false}, Cmd.none
  | RegisterResult (Error ex) -> {model with IsLoading = false; Error = Some ex.Message}, Cmd.none

// View
let view (model: Model) (dispatch: Msg -> unit) =
  Html.div [
    prop.className "min-h-screen transition-colors duration-300 bg-gray-50"
    prop.style [
      style.fontFamily "var(--font-josefin-sans)"
      style.fontSize 18
    ]
    prop.children [
      // Background image
      Html.div [
        prop.className
          "h-[200px] md:h-[300px] bg-no-repeat bg-cover bg-[url('/images/bg-mobile-light.jpg')] md:bg-[url('/images/bg-desktop-light.jpg')]"
      ]
      // Main content
      Html.main [
        prop.className "relative px-6 md:px-0 md:max-w-xl mx-auto -mt-36 md:-mt-48"
        prop.children [
          // Header
          Html.div [
            prop.className "flex justify-center items-center mb-8"
            prop.children [
              Html.h1 [
                prop.className "text-3xl md:text-4xl font-bold text-white tracking-[0.3em]"
                prop.text "TODO"
              ]
            ]
          ]
          // Register form
          Html.div [
            prop.className "rounded-md shadow-xl transition-colors duration-300 bg-white"
            prop.children [
              Html.form [
                prop.className "p-8"
                prop.onSubmit (fun ev ->
                  ev.preventDefault ()
                  dispatch AttemptRegister
                )
                prop.children [
                  Html.div [
                    prop.className "mb-6"
                    prop.children [
                      Html.label [
                        prop.className "block text-navy-850 text-sm font-bold mb-2"
                        prop.htmlFor "email"
                        prop.text "Email Address"
                      ]
                      Html.input [
                        prop.className
                          "w-full py-3 px-4 border border-gray-300 rounded-md text-navy-850 placeholder:text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        prop.id "email"
                        prop.type' "email"
                        prop.placeholder "Enter your email"
                        prop.value model.Email
                        prop.onChange (SetEmail >> dispatch)
                      ]
                    ]
                  ]
                  Html.div [
                    prop.className "mb-6"
                    prop.children [
                      Html.label [
                        prop.className "block text-navy-850 text-sm font-bold mb-2"
                        prop.htmlFor "password"
                        prop.text "Create Password"
                      ]
                      Html.input [
                        prop.className
                          "w-full py-3 px-4 border border-gray-300 rounded-md text-navy-850 placeholder:text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        prop.id "password"
                        prop.type' "password"
                        prop.placeholder "Enter your password"
                        prop.value model.Password
                        prop.onChange (SetPassword >> dispatch)
                      ]
                    ]
                  ]
                  Html.div [
                    prop.className "mb-6"
                    prop.children [
                      Html.label [
                        prop.className "block text-navy-850 text-sm font-bold mb-2"
                        prop.htmlFor "confirm-password"
                        prop.text "Confirm Password"
                      ]
                      Html.input [
                        prop.className
                          "w-full py-3 px-4 border border-gray-300 rounded-md text-navy-850 placeholder:text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        prop.id "confirm-password"
                        prop.type' "password"
                        prop.placeholder "Confirm your password"
                        prop.value model.ConfirmPassword
                        prop.onChange (SetConfirmPassword >> dispatch)
                      ]
                      match model.Error with
                      | Some error ->
                        Html.p [
                          prop.className "text-red-500 text-xs italic mt-2"
                          prop.text error
                        ]
                      | None -> Html.none
                    ]
                  ]
                  Html.div [
                    prop.className "flex flex-col gap-4"
                    prop.children [
                      Html.button [
                        prop.className
                          "w-full bg-blue-500 hover:bg-blue-600 text-white font-bold py-3 px-4 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                        prop.type' "submit"
                        prop.disabled model.IsLoading
                        prop.text (
                          if model.IsLoading then
                            "Creating Account..."
                          else
                            "Create New Account"
                        )
                      ]
                      Html.div [
                        prop.className "text-center"
                        prop.children [
                          Html.a [
                            prop.className
                              "text-blue-500 hover:text-blue-600 font-medium text-sm transition-colors duration-200"
                            prop.href "#/login"
                            prop.text "Already have an account? Login"
                          ]
                        ]
                      ]
                    ]
                  ]
                ]
              ]
            ]
          ]
        ]
      ]
    ]
  ]
