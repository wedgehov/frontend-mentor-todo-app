# Frontend Mentor - Todo app solution

This is a full-stack solution to the [Todo app challenge on Frontend Mentor](https://www.frontendmentor.io/challenges/todo-app-Su1_KokOW). Frontend Mentor challenges help you improve your coding skills by building realistic projects.

This project implements a complete Todo application with a modern F# stack for both the frontend and backend, containerized with Docker, and set up with a CI pipeline using GitHub Actions.

## Table of contents

- [Overview](#overview)
  - [The challenge](#the-challenge)
  - [Screenshot](#screenshot)
  - [Links](#links)
- [My process](#my-process)
  - [Built with](#built-with)
  - [What I learned](#what-i-learned)
  - [Continued development](#continued-development)
  - [Useful resources](#useful-resources)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Running Locally](#running-locally)
- [Author](#author)

## Overview

### The challenge

Users should be able to:

- View the optimal layout for the app depending on their device's screen size
- See hover states for all interactive elements on the page
- Add new todos to the list
- Mark todos as complete
- Delete todos from the list
- Filter by all/active/complete todos
- Clear all completed todos
- Toggle light and dark mode

- **Bonus**: Drag and drop to reorder items on the list (Note: This is not yet implemented)

- **User Authentication**: Users can register for a new account and log in.
- **Protected Routes**: The main todo list is only accessible to authenticated users.
- **Multi-page Navigation**: The application uses routing to navigate between login, registration, and the main todo page.

### Screenshot

![](./design/desktop-preview.jpg)

*(Note: Replace with an actual screenshot of the running application)*

### Links

- Solution URL: Add solution URL here
- Live Site URL: Add live site URL here

## My process

### Built with

This project is a full-stack application built entirely with F# and modern web technologies.

**Frontend:**

- F# with Fable to compile to JavaScript
- Elmish for state management (The Elm Architecture)
- Feliz for a declarative React DSL
- Vite for a fast development server and build tool
- Tailwind CSS for styling
- React as the underlying UI library
- `Fable.Elmish.UrlParser` for client-side routing

**Backend:**

- F# with ASP.NET Core
- Giraffe as a lightweight, functional web framework on top of ASP.NET Core
- Entity Framework Core for data access
- PostgreSQL for the database
- `Microsoft.AspNetCore.Authentication.Cookies` for cookie-based authentication
- `BCrypt.Net-Next` for secure password hashing

**DevOps & Tooling:**

- Docker & Docker Compose for containerization and local development
- Nginx as a reverse proxy and for serving frontend static files
- GitHub Actions for Continuous Integration (building and pushing Docker images to GitHub Container Registry)

### What I learned

This project was a great opportunity to build a complete, modern, full-stack application using F#. Some key takeaways include:

- **End-to-end F#:** Demonstrating the viability of F# for both frontend (via Fable) and backend development, enabling a consistent development experience.
- **Elmish Architecture:** Implementing a robust and predictable state management pattern on the frontend.
- **Secure Authentication Flow:** Building a complete user registration and login system with secure password hashing (BCrypt) and cookie-based sessions.
- **Client-Side Routing:** Using `Elmish.UrlParser` to create a seamless multi-page experience within a single-page application, complete with protected routes that require authentication.
- **Containerization with Docker:** Setting up a multi-container application with `docker-compose`, including a database, backend, and a frontend served by Nginx. This ensures a consistent development and deployment environment.
- **CI with GitHub Actions:** Automating the build and push process for Docker images, which is a foundational step for Continuous Deployment.

Here's a snippet from the backend showing the Giraffe route handler for user registration:

```fsharp
// backend/Program.fs

let handleRegister : HttpHandler = fun next ctx -> task {
    let db = ctx.GetService<AppDbContext>()
    let! req = ctx.BindJsonAsync<RegisterUserRequest>()

    if req = null || String.IsNullOrWhiteSpace(req.email) || String.IsNullOrWhiteSpace(req.password) then
        return! (setStatusCode 400 >=> text "Email and password are required.") next ctx
    else
        let! existingUser = findUserByEmail db req.email
        if existingUser <> null then
            return! (setStatusCode 409 >=> text "A user with that email already exists.") next ctx
        else
            let passwordHash = BCrypt.HashPassword(req.password)
            let newUser = { id = 0; email = req.email; passwordHash = passwordHash }
            db.Users.Add(newUser) |> ignore
            let! _ = db.SaveChangesAsync()
            
            // Sign in the user after successful registration
            let claims =
                [ Claim(ClaimTypes.Name, newUser.email)
                  Claim("UserId", newUser.id.ToString()) ]
            let identity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            let principal = ClaimsPrincipal(identity)
            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)
            
            return! (setStatusCode 201 >=> json newUser) next ctx
}
```

And the corresponding routing and page navigation logic on the frontend using Elmish:

```fsharp
// frontend/src/Program.fs

let urlUpdate (result: Page option) (model: Model) : Model * Cmd<Msg> =
    match result with
    | Some requestedPage ->
        let needsAuth = match requestedPage with | TodosPage -> true | _ -> false
        let isAuthenticated = model.User.IsSome

        // If user needs auth for the requested page but isn't authenticated,
        // force the page to be LoginPage. Otherwise, use the requested page.
        let actualPage =
            if needsAuth && not isAuthenticated then LoginPage
            else requestedPage

        { model with Page = actualPage }, Cmd.none
    | None -> model, Cmd.none // No page found, do nothing

// ...

let start () =
    Program.mkProgram init update view
    |> Program.toNavigable urlParser urlUpdate // Wires up the routing
    |> Program.withReactBatched "root"
    |> Program.run
```

### Continued development

Future improvements could include:

- Implementing the "drag and drop" functionality to reorder todos.
- Writing more comprehensive tests for both frontend and backend.

### Useful resources

- Fable Documentation - The official docs for the F# to JavaScript compiler.
- Elmish Documentation - Essential for understanding the state management pattern used.
- Feliz Documentation - Great resource for building React UIs with F#.
- ASP.NET Core Minimal APIs - For learning about building lightweight backends.
- Docker & Docker Compose - For understanding containerization.

## Getting Started

### Prerequisites

- Docker
- .NET 8 SDK (for local development without Docker)
- Node.js (for local development without Docker)

### Running Locally

The easiest way to run the entire application stack is with Docker Compose.

1.  Clone the repository:
    ```bash
    git clone https://github.com/your-username/your-repo-name.git
    cd your-repo-name
    ```

2.  Run the application using Docker Compose:
    ```bash
    docker-compose up --build
    ```

3.  Open your browser and navigate to:
    -   Frontend: http://localhost:5173
    -   Backend API health check: http://localhost:5199

The application will be running with the frontend communicating with the backend API inside the Docker network.

#### Viewing Logs

To view the logs from the backend service during development, run the following command in a separate terminal:

```bash
docker-compose logs -f backend
```

This will stream the backend logs to your console. Since the environment is set to `Development` in `docker-compose.yml`, the logs will be human-readable and include detailed information like SQL query parameters, which is very useful for debugging.

You can also view the frontend (Nginx) logs with `docker-compose logs -f frontend`.

## Author

- Website - Add your name here
- Frontend Mentor - @yourusername
- GitHub - @yourusername
