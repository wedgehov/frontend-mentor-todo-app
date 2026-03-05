module LoginPage

open Elmish
open Feliz
open Shared

// Model
type Model = {
  Email: string
  Password: string
  IsLoading: bool
  Error: string option
}

let init () : Model = {
  Email = ""
  Password = ""
  IsLoading = false
  Error = None
}

// Msg
type Msg =
  | SetEmail of string
  | SetPassword of string
  | AttemptLogin
  | LoginResult of Result<User, AppError>

// Update
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  match msg with
  | SetEmail email -> {model with Email = email}, Cmd.none
  | SetPassword pass -> {model with Password = pass}, Cmd.none
  | AttemptLogin ->
    {model with IsLoading = true; Error = None},
    Auth.login
      {
        Email = model.Email
        Password = model.Password
      }
      LoginResult
  | LoginResult (Ok _) ->
    // This message should be bubbled up to the main update function
    // to navigate to the todos page and store the user.
    {model with IsLoading = false}, Cmd.none
  | LoginResult (Error err) ->
    {model with IsLoading = false; Error = Some (Auth.appErrorToMessage err)}, Cmd.none

// View
let view
  theme
  (model: Model)
  (dispatch: Msg -> unit)
  (onToggleTheme: unit -> unit)
  =
  let isDark = string theme = "Dark"
  Html.div [
    prop.className ("min-h-screen transition-colors duration-300 " + if isDark then "bg-navy-950" else "bg-gray-50")
    prop.style [
      style.fontFamily "var(--font-josefin-sans)"
      style.fontSize 18
    ]
    prop.children [
      // Background image
      Html.div [
        prop.className
          ("h-[200px] md:h-[300px] bg-no-repeat bg-cover " + if isDark then "bg-[url('/images/bg-mobile-dark.jpg')] md:bg-[url('/images/bg-desktop-dark.jpg')]" else "bg-[url('/images/bg-mobile-light.jpg')] md:bg-[url('/images/bg-desktop-light.jpg')]")
      ]
      // Main content
      Html.main [
        prop.className "relative px-6 md:px-0 md:max-w-xl mx-auto -mt-36 md:-mt-48"
        prop.children [
          // Header
          Html.div [
            prop.className "relative flex justify-center items-center mb-8"
            prop.children [
              Html.h1 [
                prop.className "text-3xl md:text-4xl font-bold text-white tracking-[0.3em]"
                prop.text "TODO"
              ]
              Html.button [
                prop.className "absolute right-0 cursor-pointer"
                prop.onClick (fun _ -> onToggleTheme ())
                prop.children [
                  Html.img [
                    prop.src (if isDark then "/images/icon-sun.svg" else "/images/icon-moon.svg")
                    prop.alt "Toggle theme"
                  ]
                ]
              ]
            ]
          ]
          // Login form
          Html.div [
            prop.className ("rounded-md shadow-xl transition-colors duration-300 " + if isDark then "bg-navy-900" else "bg-white")
            prop.children [
              Html.form [
                prop.className "p-8"
                prop.onSubmit (fun ev ->
                  ev.preventDefault ()
                  AttemptLogin |> dispatch
                )
                prop.children [
                  Html.div [
                    prop.className "mb-6"
                    prop.children [
                      Html.label [
                        prop.className ("block text-sm font-bold mb-2 " + if isDark then "text-purple-300" else "text-navy-850")
                        prop.htmlFor "email"
                        prop.text "Email Address"
                      ]
                      Html.input [
                        prop.className
                          ("cursor-text w-full py-3 px-4 border rounded-md placeholder:text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent " + if isDark then "border-purple-800 bg-navy-950 text-purple-300 placeholder:text-purple-700" else "border-gray-300 text-navy-850")
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
                        prop.className ("block text-sm font-bold mb-2 " + if isDark then "text-purple-300" else "text-navy-850")
                        prop.htmlFor "password"
                        prop.text "Password"
                      ]
                      Html.input [
                        prop.className
                          ("cursor-text w-full py-3 px-4 border rounded-md placeholder:text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent " + if isDark then "border-purple-800 bg-navy-950 text-purple-300 placeholder:text-purple-700" else "border-gray-300 text-navy-850")
                        prop.id "password"
                        prop.type' "password"
                        prop.placeholder "Enter your password"
                        prop.value model.Password
                        prop.onChange (SetPassword >> dispatch)
                      ]
                      if model.Error.IsSome then
                        Html.p [
                          prop.className "text-red-500 text-xs italic mt-2"
                          prop.text model.Error.Value
                        ]
                    ]
                  ]
                  Html.div [
                    prop.className "flex flex-col gap-4"
                    prop.children [
                      Html.button [
                        prop.className
                          ("cursor-pointer w-full text-white font-bold py-3 px-4 rounded-md transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed " +
                           if isDark then
                             "bg-blue-700 hover:bg-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-2 focus:ring-offset-navy-900"
                           else
                             "bg-blue-500 hover:bg-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2")
                        prop.type' "submit"
                        prop.disabled model.IsLoading
                        prop.text (if model.IsLoading then "Logging in..." else "Login")
                      ]
                      Html.div [
                        prop.className "text-center"
                        prop.children [
                          Html.a [
                            prop.className
                              ("cursor-pointer font-medium text-sm transition-colors duration-200 " +
                               if isDark then
                                 "text-purple-300 hover:text-blue-300 underline decoration-purple-500/40 hover:decoration-blue-300"
                               else
                                 "text-blue-500 hover:text-blue-600")
                            prop.href "#/register"
                            prop.text "Don't have an account? Create one"
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
