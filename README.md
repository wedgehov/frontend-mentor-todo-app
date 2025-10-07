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
- **Database Migrations**: The database schema is managed using EF Core Migrations, allowing for version-controlled, incremental updates.

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
- **Database Migrations with EF Core:** Implementing a robust strategy for managing database schema changes, with different approaches for development and production environments.
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

### Database Migration Strategy

This project uses EF Core Migrations to manage the database schema. The strategy for applying migrations differs between development and production environments.

#### Approach 1: Automatic Migration on Startup (For Local & Dev Environments)

For local development (`docker-compose`) and the remote `dev` environment, the application is configured to automatically apply any pending migrations when it starts up. This is achieved by calling `dbContext.Database.Migrate()` in `Program.fs`.

*   **Pros:**
    *   **Simplicity & Convenience:** Extremely easy to set up and ensures the database is always in sync with the code after every deployment or restart, which is ideal for rapid iteration.
*   **Cons & Caveats:**
    *   **Not Safe for Multi-Replica Deployments:** This approach can cause race conditions and deployment failures if the application is running with more than one replica (pod in Kubernetes). The `dev` environment **must** be configured to run a single replica for this to be safe.
    *   **Requires Elevated Permissions:** The application's database user needs permissions to alter the schema (`ALTER`, `CREATE`), which is not ideal from a security perspective.
    *   **Hides Production Deployment Flow:** The deployment process for `dev` is different from how `test` and `prod` should be handled, which can mask potential issues.

#### Approach 2: Kubernetes Job for Migrations (For Test, Staging & Production)

For production-like environments, the recommended best practice is to decouple migration from the application startup.

*   **How it Works:** The CI/CD pipeline first deploys a short-lived Kubernetes `Job` whose only task is to run the database migrations. Only after this job completes successfully does the pipeline proceed to deploy the new version of the main application.
*   **Pros:**
    *   **Safe for Production:** Eliminates race conditions, making it safe for multi-replica, high-availability setups.
    *   **Improved Security:** Allows the migration job to use a database account with elevated permissions, while the main application runs with a less-privileged account that cannot alter the schema.
    *   **Controlled Deployments:** Makes schema migration an explicit, visible step in the deployment process. If it fails, the application deployment is aborted, preventing the app from running against an incorrect schema.

This project currently uses **Approach 1** for simplicity in the development phase. The transition to **Approach 2** is a planned step for when `test` and `prod` environments are configured.

**Runtime Note (dev/docker-compose):** To ensure EF Core can discover the migrations assembly inside the Docker container, the application proactively loads the `Backend.DbMigrations.dll` at startup if it exists. This is a pragmatic workaround for the development environment. For Kubernetes test/staging/prod environments, migrations should be run via a dedicated Job before the application deploys, which is the recommended best practice.

### Continued development

Future improvements could include:

- Implementing the "drag and drop" functionality to reorder todos.
- Writing more comprehensive tests for both frontend and backend.
- Implementing the Kubernetes Job pattern (Approach 2) for database migrations as `test` and `prod` environments are introduced.

### Useful resources

- Fable Documentation - The official docs for the F# to JavaScript compiler.
- Elmish Documentation - Essential for understanding the state management pattern used.
- Feliz Documentation - Great resource for building React UIs with F#.
- ASP.NET Core Minimal APIs - For learning about building lightweight backends.
- Docker & Docker Compose - For understanding containerization.

## Getting Started

### Prerequisites

- Docker
- .NET 9 SDK (for local development without Docker)
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

On the first run, the backend service will automatically apply database migrations and seed a test user (`test@example.com` with password `secret123`).

### Managing Database Migrations

EF Core tools (`dotnet-ef`) are used to create and manage migrations. The tools are installed as a local tool in the repository.

1.  **Install EF Core Tools (if not already installed):**
    ```bash
    dotnet new tool-manifest
    dotnet tool install dotnet-ef
    ```

2.  **Create a New Migration:**

    After making changes to your entity models in `backend/Program.fs`, create a new migration by running the following command from the root of the repository:

    ```bash
    dotnet ef migrations add YourMigrationName --project backend/DbMigrations --startup-project backend
    ```

    This command tells `dotnet-ef` to:
    - Compare the model against the last migration.
    - Scaffold a new migration in the `backend/DbMigrations` project.
    - Use the `backend` project's configuration to do so.

The new migration will be applied automatically the next time the application starts in your development environment.

### Viewing Logs

To view the logs from the backend service during development, run the following command in a separate terminal:

```bash
docker-compose logs -f backend
```

This will stream the backend logs to your console. Since the environment is set to `Development`