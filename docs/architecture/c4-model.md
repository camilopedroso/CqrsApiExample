# CqrsApiExample: C4 model

*Português: [c4-model.pt-br.md](c4-model.pt-br.md)*

Architecture diagrams following the [C4 model](https://c4model.com): system context, containers,
components and code, plus a dynamic view of `POST /api/users` and the local deployment.
The diagrams are [Mermaid C4](https://mermaid.js.org/syntax/c4.html), which GitHub and most
Markdown previews render.

For the rules behind each box (why there is no mediator, how the outbox guarantees delivery,
how the provider flag works) see `CLAUDE.md`.

## Level 1: System context

Who uses the system and what it talks to.

```mermaid
C4Context
    title System context: CqrsApiExample

    Person(client, "API client", "A front end, another service or a developer using Swagger / the .rest file.")

    System(cqrsApi, "CqrsApiExample", "Users API: creates and queries users, sends a welcome email when a user is created.")

    System_Ext(email, "Email provider", "SMTP or an email service. Not wired yet: LoggingEmailSender only logs the email.")
    System_Ext(observability, "Logs and metrics", "Console logs; the CqrsApiExample.Persistence meter (dotnet-counters / OpenTelemetry).")

    Rel(client, cqrsApi, "Creates and reads users", "HTTPS, JSON / ProblemDetails")
    Rel(cqrsApi, email, "Sends welcome emails", "IEmailSender (planned)")
    Rel(cqrsApi, observability, "Writes logs and persistence metrics")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

## Level 2: Containers

The system is one ASP.NET Core process and one SQL Server database. The outbox worker is a
hosted service **inside** the API process, not a separate deployable.

```mermaid
C4Container
    title Containers: CqrsApiExample

    Person(client, "API client", "Calls the REST API.")

    System_Boundary(system, "CqrsApiExample") {
        Container(api, "Web API", ".NET 10, ASP.NET Core", "REST endpoints under /api/users, OpenAPI + Swagger UI in Development. Hosts the outbox background service in the same process.")
        ContainerDb(db, "Database", "SQL Server 2022, database CqrsApi", "Schema Accounts: Users. Schema Messaging: OutboxMessages, OutboxMessageConsumers. Schema owned by the EF Core migrations.")
    }

    System_Ext(email, "Email provider", "Not wired yet (LoggingEmailSender).")

    Rel(client, api, "GET/POST /api/users", "HTTPS, JSON")
    Rel(api, db, "Writes users + outbox rows in one transaction; reads users; leases outbox batches", "TDS: EF Core or ADO.NET (Persistence:Provider)")
    Rel(api, email, "Welcome email, from the outbox", "IEmailSender")

    UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="1")
```

| Container | Technology | Responsibility |
|---|---|---|
| Web API | .NET 10, ASP.NET Core (projects Api, Contracts, Application, Domain, Infrastructure) | HTTP endpoints, use cases (commands/queries), domain model, persistence, outbox worker |
| Database | SQL Server 2022 (`cqrs-api-example-sql`, host port 14330 locally) | Users (`[Accounts].[Users]`) and the transactional outbox (`[Messaging].*`) |

## Level 3: Components of the Web API

Grouped by project (Clean Architecture: dependencies point inward, to the Domain).
The controller receives the handler through `[FromServices]`; there is no mediator.

```mermaid
C4Component
    title Components: Web API

    Person(client, "API client")
    ContainerDb(db, "Database", "SQL Server")
    System_Ext(email, "Email provider")

    Container_Boundary(apiLayer, "Api + Contracts") {
        Component(usersController, "UsersController", "ASP.NET Core controller", "Maps contracts to commands/queries, calls one handler per action, Match → 200/201 or ProblemDetails.")
        Component(errorHandling, "Error responses", "GlobalExceptionHandler, CqrsApiExampleProblemDetailsFactory", "Every error is ProblemDetails with errorCodes; unexpected exceptions → 500 / 499.")
    }

    Container_Boundary(appLayer, "Application") {
        Component(pipeline, "Request pipeline", "LoggingDecorator → AuthorizationDecorator → ValidationDecorator", "Wraps every IRequestHandler: logs duration and error codes, runs the FluentValidation validator.")
        Component(commands, "Command handlers", "CreateUserCommandHandler", "Builds the aggregate, checks the email, commits through IUnitOfWork.")
        Component(queries, "Query handlers", "GetAllUsers, GetUserById", "Read through IUserQueries only, return UserDto.")
        Component(dispatcher, "DomainEventDispatcher", "IDomainEventDispatcher", "Finds the IDomainEventHandler<T> of an event.")
        Component(eventHandlers, "Domain event handlers", "SendWelcomeEmailWhenUserCreated", "React to events delivered by the outbox.")
    }

    Container_Boundary(domainLayer, "Domain") {
        Component(user, "User aggregate", "AggregateRoot, Result<T>, Errors", "Enforces invariants in User.Create, raises UserCreatedDomainEvent.")
    }

    Container_Boundary(infraLayer, "Infrastructure") {
        Component(writeSide, "Write side", "IUserRepository + IUnitOfWork (EF Core or ADO.NET)", "Tracks aggregates; SaveChanges stores them and their outbox rows in one transaction.")
        Component(readSide, "Read side", "IUserQueries (EF Core AsNoTracking or ADO.NET)", "Projects straight to UserDto.")
        Component(instrumentation, "Persistence instrumentation", "*InstrumentationDecorator", "Logs and measures every persistence call, whatever the provider.")
        Component(outbox, "Outbox", "OutboxBackgroundService + OutboxProcessor (EF Core)", "Polls, leases batches, runs handlers, retries with backoff, dead letters.")
        Component(emailSender, "LoggingEmailSender", "IEmailSender", "Logs the email instead of sending it.")
    }

    Rel(client, usersController, "HTTP requests", "JSON")
    Rel(usersController, pipeline, "Handle(command/query)")
    Rel(pipeline, commands, "Calls")
    Rel(pipeline, queries, "Calls")
    Rel(commands, user, "User.Create")
    Rel(commands, instrumentation, "IUserRepository, IUnitOfWork")
    Rel(queries, instrumentation, "IUserQueries")
    Rel(instrumentation, writeSide, "Delegates to")
    Rel(instrumentation, readSide, "Delegates to")
    Rel(writeSide, db, "INSERT Users + OutboxMessages")
    Rel(readSide, db, "SELECT")
    Rel(outbox, db, "UPDATE … OUTPUT lease, consumers")
    Rel(outbox, dispatcher, "GetInvocations(event)")
    Rel(dispatcher, eventHandlers, "Resolves")
    Rel(eventHandlers, emailSender, "SendAsync")
    Rel(emailSender, email, "Planned")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="2")
```

`errorHandling` has no arrows on purpose: it is ASP.NET Core middleware and factories that
answer any exception or error the controller and the framework produce.

| Component | Where | Notes |
|---|---|---|
| UsersController | `src/Api/Controllers/UsersController.cs` | `[FromServices] IRequestHandler<TRequest, Result<T>>` per action |
| Error responses | `src/Api/Common/Errors/` | `ErrorType` → 400/404/409/500; `RequestErrors` for routing errors |
| Request pipeline | `src/Application/Common/Decorators/`, `src/Application/DependencyInjection.cs` | Decorators built at startup by assembly scanning |
| Command handlers | `src/Application/Users/Commands/` | Return `Result<UserDto>`, never entities |
| Query handlers | `src/Application/Users/Queries/` | Never use `IUserRepository` (architecture test) |
| Domain event dispatch | `src/Application/Common/Events/`, `src/Application/Users/Events/` | Handlers must tolerate repeats (at-least-once) |
| User aggregate | `src/Domain/UserAggregate/` | Package-free, persistence-ignorant |
| Write / read side | `src/Infrastructure/Persistence/` (`Repositories`, `Queries`, `Ado/`) | `Persistence:Provider` = `Ado` or `EfCore` |
| Instrumentation | `src/Infrastructure/Persistence/Decorators/`, `Instrumentation/` | Histogram `cqrsapi.persistence.operation.duration` |
| Outbox | `src/Infrastructure/Outbox/` | Always EF Core, `UPDLOCK, READPAST` leases |

## Dynamic view: `POST /api/users`

The request returns as soon as the user and its event are committed; the welcome email is sent
later by the outbox, in another scope.

```mermaid
C4Dynamic
    title Dynamic: creating a user

    Person(client, "API client")
    ContainerDb(db, "Database", "SQL Server")

    Container_Boundary(api, "Web API") {
        Component(controller, "UsersController")
        Component(pipeline, "LoggingDecorator → AuthorizationDecorator → ValidationDecorator")
        Component(handler, "CreateUserCommandHandler")
        Component(user, "User aggregate")
        Component(uow, "IUserRepository + IUnitOfWork")
        Component(outbox, "OutboxProcessor")
        Component(welcome, "SendWelcomeEmailWhenUserCreated")
    }

    Rel(client, controller, "1. POST /api/users", "JSON")
    Rel(controller, pipeline, "2. Handle(CreateUserCommand)")
    Rel(pipeline, handler, "3. Valid input → Handle")
    Rel(handler, user, "4. User.Create raises UserCreatedDomainEvent")
    Rel(handler, uow, "5. ExistsByEmail, Add, SaveChangesAsync")
    Rel(uow, db, "6. INSERT user + outbox row, one transaction")
    Rel(controller, client, "7. 201 Created + Location")
    Rel(outbox, db, "8. Lease due messages (every 2 s)")
    Rel(outbox, welcome, "9. Run handler, record consumer")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

Failure paths: invalid input stops at step 3 (400 `User.Invalid*`); a duplicate email stops at
step 5 or, under concurrency, at step 6 through `UniqueConstraintViolationException` (409
`User.DuplicateEmail`, nothing stored); a failing event handler is retried with exponential
backoff and dead-lettered after 10 attempts.

## Deployment: local Docker Compose

```mermaid
C4Deployment
    title Deployment: docker compose (local development)

    Deployment_Node(dev, "Developer machine", "macOS / Linux / Windows, Docker") {
        Deployment_Node(compose, "Compose project cqrs-api-example") {
            Deployment_Node(apiNode, "cqrs-api-example-api", "mcr.microsoft.com/dotnet/aspnet:10.0, non-root") {
                Container(api, "Web API", "ASP.NET Core, port 8080", "ASPNETCORE_ENVIRONMENT=Development: applies migrations, seeds, Swagger.")
            }
            Deployment_Node(dbNode, "cqrs-api-example-sql", "mcr.microsoft.com/mssql/server:2022-latest, linux/amd64") {
                ContainerDb(db, "CqrsApi", "SQL Server 2022, port 1433", "Volume mssql-data.")
            }
        }
    }

    Rel(api, db, "Server=db,1433", "TDS")

    UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="1")
```

Host ports bind to `127.0.0.1` only: the API on **5235** (→ 8080) and SQL Server on **14330**
(→ 1433, `MSSQL_HOST_PORT`). The API waits for the database healthcheck. Outside Development
nothing is migrated automatically.

## Level 4: Code (selected)

Only the parts worth a picture: the domain model and the persistence abstractions the
Application owns. Everything else is better read in the code.

```mermaid
classDiagram
    direction LR

    class Entity {
        <<abstract>>
        +Guid Id
        +DateTime CreatedAt
        +DateTime? ModifiedAt
    }
    class AggregateRoot {
        <<abstract>>
        +GetDomainEvents()
        +ClearDomainEvents()
        #RaiseDomainEvent(IDomainEvent)
    }
    class User {
        +string Name
        +string Surname
        +string Email
        +Create(name, surname, email)$ Result~User~
    }
    class UserCreatedDomainEvent {
        <<record>>
        +Guid UserId
        +string Name
        +string Surname
        +string Email
    }
    class IUserRepository {
        <<interface>>
        +ExistsByEmailAsync(email, ct)
        +Add(User)
    }
    class IUnitOfWork {
        <<interface>>
        +SaveChangesAsync(ct)
    }
    class IUserQueries {
        <<interface>>
        +GetAllAsync(ct) List~UserDto~
        +GetByIdAsync(id, ct) UserDto
    }

    Entity <|-- AggregateRoot
    AggregateRoot <|-- User
    User ..> UserCreatedDomainEvent : raises
    IUserRepository ..> User : tracks
    IUserQueries ..> UserDto : projects to

    IUserRepository <|.. UserRepository : EF Core
    IUserRepository <|.. AdoUserRepository : ADO.NET
    IUnitOfWork <|.. UnitOfWork : EF Core
    IUnitOfWork <|.. AdoUnitOfWork : ADO.NET
    IUserQueries <|.. UserQueries : EF Core
    IUserQueries <|.. AdoUserQueries : ADO.NET
```
