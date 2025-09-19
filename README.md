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

**Backend:**

- F# with ASP.NET Core Minimal APIs
- PostgreSQL for the database
- Npgsql as the .NET data provider for PostgreSQL

**DevOps & Tooling:**

- Docker & Docker Compose for containerization and local development
- Nginx as a reverse proxy and for serving frontend static files
- GitHub Actions for Continuous Integration (building and pushing Docker images to GitHub Container Registry)

### What I learned

This project was a great opportunity to build a complete, modern, full-stack application using F#. Some key takeaways include:

- **End-to-end F#:** Demonstrating the viability of F# for both frontend (via Fable) and backend development, enabling a consistent development experience.
- **Elmish Architecture:** Implementing a robust and predictable state management pattern on the frontend.
- **Minimal APIs in F#:** Creating a lightweight and performant backend API with ASP.NET Core.
- **Containerization with Docker:** Setting up a multi-container application with `docker-compose`, including a database, backend, and a frontend served by Nginx. This ensures a consistent development and deployment environment.
- **CI with GitHub Actions:** Automating the build and push process for Docker images, which is a foundational step for Continuous Deployment.

Here's a snippet of the F# backend defining a minimal API endpoint:

```fsharp
// backend/Program.fs

let getTodos () : IResult =
    use cmd = dataSource.CreateCommand("select id, text, completed from todos order by id;")
    use r = cmd.ExecuteReader()
    let acc = ResizeArray<Todo>()
    while r.Read() do
        acc.Add({ Id = r.GetInt32(0); Text = r.GetString(1); Completed = r.GetBoolean(2) })
    Results.Ok(acc)

// ...

app.MapGet ("/api/todos", Func<IResult>(getTodos)) |> ignore
```

And the corresponding Elmish update logic on the frontend:

```fsharp
// frontend/src/Program.fs

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  match msg with
  // ...
  | GotTodos (Ok todos) ->
      { model with Todos = Loaded todos }, Cmd.none
  | GotTodos (Error e) ->
      { model with Todos = Errored e.Message }, Cmd.none
  // ...
```

### Continued development

Future improvements could include:

- Implementing the "drag and drop" functionality to reorder todos.
- Adding user authentication to support multiple users.
- Writing more comprehensive tests for both frontend and backend.
- Setting up a Continuous Deployment (CD) pipeline to automatically deploy the application.

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
    -   Backend API health check: http://localhost:5199/api/health

The application will be running with the frontend communicating with the backend API inside the Docker network.

## Author

- Website - Add your name here
- Frontend Mentor - @yourusername
- GitHub - @yourusername
