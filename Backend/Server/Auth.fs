module Auth

open System
open System.Security.Claims
open BCrypt.Net
open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.EntityFrameworkCore
open System.Linq
open System.Threading.Tasks
open Shared

let private hasAuthenticatedIdentity (user: ClaimsPrincipal) =
    user.Identity
    |> Option.ofObj
    |> Option.exists (fun identity -> identity.IsAuthenticated)

let requiresAuthentication: HttpHandler =
    fun next ctx ->
        if hasAuthenticatedIdentity ctx.User then
            next ctx
        else
            (setStatusCode 401 >=> text "User not authenticated.") next ctx

let tryGetUserId (ctx: HttpContext) : Result<int, AppError> =
    ctx.User.FindFirst "UserId"
    |> Option.ofObj
    |> Option.bind (fun claim -> Option.tryParse<int> claim.Value)
    |> Result.requireSome Unauthorized

let private toSharedUser (user: Entity.User) : User = {
    Id = user.Id
    Email = user.Email
}

let private fromTask (work: Task<'a>) : Async<Result<'a, AppError>> =
    work |> Async.AwaitTask |> AsyncResult.ofAsync

let private findUserByEmail (db: Entity.AppDbContext) (email: string) =
    db.Users.AsNoTracking().FirstOrDefaultAsync(fun u -> u.Email = email)

let private requireEmailAndPassword error email password =
    (String.IsNullOrWhiteSpace(email) || String.IsNullOrWhiteSpace(password))
    |> Result.requireFalse error

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
    ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)
    |> Async.AwaitTask
    |> AsyncResult.ofAsync

let private register (ctx: HttpContext) (db: Entity.AppDbContext) (req: RegisterRequest) =
    asyncResult {
        do! requireEmailAndPassword (ValidationError "Email and password are required.") req.Email req.Password

        do!
            findUserByEmail db req.Email
            |> fromTask
            |> AsyncResult.map Option.ofObj
            |> AsyncResult.bindRequireNone Conflict

        let passwordHash = BCrypt.HashPassword(req.Password)
        let newUser = Entity.User()
        newUser.Email <- req.Email
        newUser.PasswordHash <- passwordHash
        db.Users.Add(newUser) |> ignore

        do! db.SaveChangesAsync() |> fromTask |> AsyncResult.ignore
        do! signInUser ctx newUser

        return toSharedUser newUser
    }

let private login (ctx: HttpContext) (db: Entity.AppDbContext) (req: LoginRequest) =
    asyncResult {
        do! requireEmailAndPassword InvalidCredentials req.Email req.Password

        let! user =
            findUserByEmail db req.Email
            |> fromTask
            |> AsyncResult.map Option.ofObj
            |> AsyncResult.bindRequireSome InvalidCredentials

        do!
            BCrypt.Verify(req.Password, user.PasswordHash)
            |> Result.requireTrue InvalidCredentials

        do! signInUser ctx user
        return toSharedUser user
    }

let private logout (ctx: HttpContext) () =
    asyncResult {
        do!
            ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            |> Async.AwaitTask
            |> AsyncResult.ofAsync
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
