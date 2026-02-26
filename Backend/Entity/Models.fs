namespace Entity

open System.ComponentModel.DataAnnotations
open System.ComponentModel.DataAnnotations.Schema

[<CLIMutable; Table("users")>]
type User = {
    [<Key; Column("id")>]
    Id: int
    [<Required; Column("email")>]
    Email: string
    [<Required; Column("password_hash")>]
    PasswordHash: string
}

[<CLIMutable; Table("todos")>]
type Todo = {
    [<Key; Column("id")>]
    Id: int
    [<Required; Column("text")>]
    mutable Text: string
    [<Column("completed")>]
    mutable Completed: bool
    [<Column("user_id")>]
    UserId: int
}
