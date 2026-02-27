module Auth

open System
open System.Security.Claims
open BCrypt.Net
open Data
open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open System.Threading.Tasks
open Shared

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

let tryGetUserId (ctx: HttpContext) : Result<int, AppError> =
    let user = ctx.User

    if isNull (box user) then
        Error Unauthorized
    else
        match user.FindFirst "UserId" with
        | null -> Error Unauthorized
        | claim ->
            match Int32.TryParse claim.Value with
            | true, userId -> Ok userId
            | false, _ -> Error Unauthorized

let private toSharedUser (user: Entity.User) : User = {
    Id = user.Id
    Email = user.Email
}

let private fromTask (work: Task<'a>) =
    work |> Async.AwaitTask |> AsyncResult.ofAsync

let private fromTaskUnit (work: Task) =
    work |> Async.AwaitTask |> AsyncResult.ofAsync

let private createPrincipal (email: string) (userId: int) =
    let claims = [
        Claim(ClaimTypes.Name, email)
        Claim("UserId", userId.ToString())
    ]

    let identity =
        ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)

    ClaimsPrincipal(identity)

let private signInUser (ctx: HttpContext) (user: Entity.User) =
    let principal = createPrincipal user.Email user.Id
    ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal) |> fromTaskUnit

let private register (ctx: HttpContext) (db: Entity.AppDbContext) (req: RegisterRequest) =
    asyncResult {
        if String.IsNullOrWhiteSpace(req.Email) || String.IsNullOrWhiteSpace(req.Password) then
            return! Error(ValidationError "Email and password are required.")

        let! existingUser = findUserByEmail db req.Email |> fromTask

        if not (isNull existingUser) then
            return! Error Conflict

        let passwordHash = BCrypt.HashPassword(req.Password)
        let newUser = Entity.User()
        newUser.Email <- req.Email
        newUser.PasswordHash <- passwordHash
        db.Users.Add(newUser) |> ignore

        do! db.SaveChangesAsync() |> fromTask |> AsyncResult.map ignore
        do! signInUser ctx newUser

        return toSharedUser newUser
    }

let private login (ctx: HttpContext) (db: Entity.AppDbContext) (req: LoginRequest) =
    asyncResult {
        if String.IsNullOrWhiteSpace(req.Email) || String.IsNullOrWhiteSpace(req.Password) then
            return! Error InvalidCredentials

        let! user = findUserByEmail db req.Email |> fromTask

        match Option.ofObj user with
        | None -> return! Error InvalidCredentials
        | Some user ->
            if not (BCrypt.Verify(req.Password, user.PasswordHash)) then
                return! Error InvalidCredentials

            do! signInUser ctx user
            return toSharedUser user
    }

let private logout (ctx: HttpContext) () =
    asyncResult {
        do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme) |> fromTaskUnit
        return ()
    }

let authApiImplementation (ctx: HttpContext) : IAuthApi =
    let db = ctx.GetService<Entity.AppDbContext>()

    {
        Register = register ctx db
        Login = login ctx db
        Logout = logout ctx
    }

let authApiHandler: HttpHandler =
    RemotingUtil.handlerFromApi authApiImplementation
