namespace Entity

open Microsoft.EntityFrameworkCore

type AppDbContext(options: DbContextOptions<AppDbContext>) =
    inherit DbContext(options)

    [<DefaultValue(false)>]
    val mutable private users: DbSet<User>

    member this.Users
        with get () = this.users
        and set value = this.users <- value

    [<DefaultValue(false)>]
    val mutable private todos: DbSet<Todo>

    member this.Todos
        with get () = this.todos
        and set value = this.todos <- value
