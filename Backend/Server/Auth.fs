module Auth

open System
open System.Security.Claims
open System.Threading.Tasks
open BCrypt.Net
open Dtos
open Data
open Entity
open Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection

let requiresAuthentication: HttpHandler =
    fun next ctx ->
        let user = ctx.User

        let isAuthenticated =
            if isNull (box user) then
                false
            else
                match user.Identity with
                | null -> false
                | identity -> identity.IsAuthenticated

        if isAuthenticated then
            next ctx
        else
            (setStatusCode 401 >=> text "User not authenticated.") next ctx

let getUserId (ctx: HttpContext) : int =
    let user = ctx.User

    if isNull (box user) then
        failwith
            "getUserId was called on an unauthenticated HttpContext. This indicates a programming error where an authenticated route did not use the 'requiresAuthentication' handler."
    else
        match user.FindFirst "UserId" with
        | null ->
            failwith
                "UserId claim not found in token. This is unexpected for an authenticated user."
        | claim -> Int32.Parse claim.Value

let handleRegister: HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let! req = ctx.BindJsonAsync<RegisterUserRequest>()

            if String.IsNullOrWhiteSpace(req.email) || String.IsNullOrWhiteSpace(req.password) then
                return! (setStatusCode 400 >=> text "Email and password are required.") next ctx
            else
                let! existingUser = findUserByEmail db req.email

                if existingUser <> null then
                    return!
                        (setStatusCode 409 >=> text "A user with that email already exists.")
                            next
                            ctx
                else
                    let passwordHash = BCrypt.HashPassword(req.password)
                    let newUser = {
                        Id = 0
                        Email = req.email
                        PasswordHash = passwordHash
                    }
                    db.Users.Add(newUser) |> ignore
                    let! _ = db.SaveChangesAsync()

                    let claims = [
                        Claim(ClaimTypes.Name, newUser.Email)
                        Claim("UserId", newUser.Id.ToString())
                    ]

                    let identity =
                        ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                    let principal = ClaimsPrincipal(identity)
                    do!
                        ctx.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            principal
                        )

                    return! (setStatusCode 201 >=> json newUser) next ctx
        }

let handleLogin: HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<AppDbContext>()
            let! req = ctx.BindJsonAsync<LoginUserRequest>()

            if String.IsNullOrWhiteSpace(req.email) || String.IsNullOrWhiteSpace(req.password) then
                return! (setStatusCode 401 >=> text "Invalid credentials.") next ctx
            else
                let! user = findUserByEmail db req.email

                match Option.ofObj user with
                | None -> return! (setStatusCode 401 >=> text "Invalid credentials.") next ctx
                | Some user ->
                    if BCrypt.Verify(req.password, user.PasswordHash) then
                        let claims = [
                            Claim(ClaimTypes.Name, user.Email)
                            Claim("UserId", user.Id.ToString())
                        ]

                        let identity =
                            ClaimsIdentity(
                                claims,
                                CookieAuthenticationDefaults.AuthenticationScheme
                            )

                        let principal = ClaimsPrincipal(identity)
                        do!
                            ctx.SignInAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme,
                                principal
                            )
                        return! json user next ctx
                    else
                        return! (setStatusCode 401 >=> text "Invalid credentials.") next ctx
        }

let handleLogout: HttpHandler =
    fun next ctx ->
        task {
            do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            return! (setStatusCode 204) next ctx
        }
