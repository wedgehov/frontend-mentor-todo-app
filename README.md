# Frontend Mentor - Todo app solution

This is a full-stack solution to the [Todo app challenge on Frontend Mentor](https://www.frontendmentor.io/challenges/todo-app-Su1_KokOW). Frontend Mentor challenges help you improve your coding skills by building realistic projects.

This project implements a complete Todo application with a modern F# stack for both the frontend and backend, containerized with Docker, and set up with a CI pipeline using GitHub Actions.

## Table of contents

- [Overview](#overview)
  - [The challenge](#the-challenge)
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

- **Bonus**: Drag and drop to reorder items on the list

- **User Authentication**: Users can register for a new account and log in.
- **Protected Routes**: The main todo list is only accessible to authenticated users.
- **Multi-page Navigation**: The application uses routing to navigate between login, registration, and the main todo page.
- **Database Migrations**: The database schema is managed using EF Core Migrations, allowing for version-controlled, incremental updates.
- **Todo Ordering Strategy**: Reordering persists via an integer `position` per todo. Moving an item uses an `O(n)` shift (increment/decrement affected rows) for simplicity. Order-key strategies (for example LexoRank/fractional keys) were considered, but deferred to keep this app easier to reason about.

### Links

- Solution URL: [GitHub Repository](https://github.com/wedgehov/frontend-mentor-todo-app)
- Live Site URL: [https://fm-todo-app-main.vhovet.com](https://fm-todo-app-main.vhovet.com)

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
- GitHub Actions for Continuous Integration (building and pushing Docker images to GitHub Container Registry)

### What I learned

One of the most interesting challenges in this project was implementing the drag-and-drop list reordering feature. I chose to handle the ordering by assigning an integer `position` to each todo item. When a user drags and drops an item, the application calculates the new position and performs an `O(n)` shift on the backend, incrementing or decrementing the positions of the affected rows.

While this approach is straightforward and easy to reason about for a small-scale application, it presented some challenges in ensuring the UI state and the backend state remained perfectly synchronized during the drag-and-drop operation. I had to carefully align the index contract between the Elmish frontend and the Giraffe backend so that the final target index used by the UI reorder matched the `MoveTodo` API payload.

### Continued development

Future improvements could include:

- Enhancing drag-and-drop interactions (for example drop-zones/animations) and evaluating order-key ranking (like LexoRank or fractional keys) if list scale or concurrency grows.
- Writing more comprehensive tests for both frontend and backend.
- Implementing a robust Kubernetes Job pattern for database migrations as `test` and `prod` environments are introduced, decoupling migration from the application startup to ensure safety in multi-replica deployments.

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
    git clone https://github.com/wedgehov/frontend-mentor-todo-app.git
    cd frontend-mentor-todo-app
    ```

2.  Run the application using Docker Compose:
    ```bash
    docker-compose up --build
    ```

3.  Open your browser and navigate to:
    -   Application: http://localhost:5199
    -   Backend API health check: http://localhost:5199/health

On the first run, the backend service will automatically apply database migrations and seed a test user. You can log in with the following credentials:
- **Email:** `test@example.com`
- **Password:** `secret123`

#### Frontend-only workflow (npm)

If you only want to run or build the frontend locally:

```bash
cd Client
npm install
npm run dev
# or
npm run build
```

### Managing Database Migrations

EF Core tools (`dotnet-ef`) are used to create and manage migrations. The tools are installed as a local tool in the repository.

#### Initial Setup (One-Time)

If you haven't done so, install the local `dotnet-ef` tool:
```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

#### Workflow for Schema Changes

When you make changes to your entity models in `Backend/Entity` (e.g., adding a property to `Todo.cs`), follow these steps to create and apply a new migration:

1.  **Create a New Migration:**

    Run the following command from the root of the repository. This will generate a new C# migration file in `Backend/Entity/Migrations`.

    ```bash
    dotnet ef migrations add YourMigrationName --project Backend/Entity/Entity.csproj --startup-project Backend/Server/backend.fsproj --output-dir Migrations
    ```

2.  **Apply the Migration:**

    The new migration will be applied automatically the next time you start the application with `docker-compose up`. The retry logic at startup will handle applying the new schema to the database.

### Viewing Logs

To view the logs from the backend service during development, run the following command in a separate terminal:

```bash
docker-compose logs -f backend
```

This will stream the backend logs to your console.
