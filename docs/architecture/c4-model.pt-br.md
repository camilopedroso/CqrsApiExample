# CqrsApiExample: modelo C4

*English: [c4-model.md](c4-model.md)*

Diagramas de arquitetura seguindo o [modelo C4](https://c4model.com): contexto do sistema,
contêineres, componentes e código, além de uma visão dinâmica do `POST /api/users` e do
deployment local. Os diagramas usam [Mermaid C4](https://mermaid.js.org/syntax/c4.html), que o
GitHub e a maioria dos visualizadores de Markdown renderizam.

As regras por trás de cada caixa (por que não há mediator, como o outbox garante a entrega,
como funciona a flag de provider) estão no `CLAUDE.md`.

## Nível 1: Contexto do sistema

Quem usa o sistema e com o que ele se comunica.

```mermaid
C4Context
    title Contexto do sistema: CqrsApiExample

    Person(client, "Cliente da API", "Um front-end, outro serviço ou um desenvolvedor usando o Swagger / o arquivo .rest.")

    System(cqrsApi, "CqrsApiExample", "API de usuários: cria e consulta usuários e envia um e-mail de boas-vindas quando um usuário é criado.")

    System_Ext(email, "Provedor de e-mail", "SMTP ou um serviço de e-mail. Ainda não integrado: o LoggingEmailSender só registra o e-mail no log.")
    System_Ext(observability, "Logs e métricas", "Logs no console; o meter CqrsApiExample.Persistence (dotnet-counters / OpenTelemetry).")

    Rel(client, cqrsApi, "Cria e consulta usuários", "HTTPS, JSON / ProblemDetails")
    Rel(cqrsApi, email, "Envia e-mails de boas-vindas", "IEmailSender (planejado)")
    Rel(cqrsApi, observability, "Grava logs e métricas de persistência")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

## Nível 2: Contêineres

O sistema é um processo ASP.NET Core e um banco SQL Server. O worker do outbox é um hosted
service **dentro** do processo da API, não algo publicado à parte.

```mermaid
C4Container
    title Contêineres: CqrsApiExample

    Person(client, "Cliente da API", "Chama a API REST.")

    System_Boundary(system, "CqrsApiExample") {
        Container(api, "Web API", ".NET 10, ASP.NET Core", "Endpoints REST em /api/users, OpenAPI + Swagger UI em Development. Hospeda o serviço de outbox em segundo plano no mesmo processo.")
        ContainerDb(db, "Banco de dados", "SQL Server 2022, banco CqrsApi", "Schema Accounts: Users. Schema Messaging: OutboxMessages, OutboxMessageConsumers. Schema controlado pelas migrations do EF Core.")
    }

    System_Ext(email, "Provedor de e-mail", "Ainda não integrado (LoggingEmailSender).")

    Rel(client, api, "GET/POST /api/users", "HTTPS, JSON")
    Rel(api, db, "Grava usuários + linhas do outbox numa transação; lê usuários; reserva lotes do outbox", "TDS: EF Core ou ADO.NET (Persistence:Provider)")
    Rel(api, email, "E-mail de boas-vindas, a partir do outbox", "IEmailSender")

    UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="1")
```

| Contêiner | Tecnologia | Responsabilidade |
|---|---|---|
| Web API | .NET 10, ASP.NET Core (projetos Api, Contracts, Application, Domain, Infrastructure) | Endpoints HTTP, casos de uso (commands/queries), modelo de domínio, persistência, worker do outbox |
| Banco de dados | SQL Server 2022 (`cqrs-api-example-sql`, porta 14330 no host local) | Usuários (`[Accounts].[Users]`) e o outbox transacional (`[Messaging].*`) |

## Nível 3: Componentes da Web API

Agrupados por projeto (Clean Architecture: as dependências apontam para dentro, para o Domain).
O controller recebe o handler via `[FromServices]`; não há mediator.

```mermaid
C4Component
    title Componentes: Web API

    Person(client, "Cliente da API")
    ContainerDb(db, "Banco de dados", "SQL Server")
    System_Ext(email, "Provedor de e-mail")

    Container_Boundary(apiLayer, "Api + Contracts") {
        Component(usersController, "UsersController", "Controller ASP.NET Core", "Converte contratos em commands/queries, chama um handler por action, Match → 200/201 ou ProblemDetails.")
        Component(errorHandling, "Respostas de erro", "GlobalExceptionHandler, CqrsApiExampleProblemDetailsFactory", "Todo erro é ProblemDetails com errorCodes; exceções inesperadas → 500 / 499.")
    }

    Container_Boundary(appLayer, "Application") {
        Component(pipeline, "Pipeline de requisições", "LoggingDecorator → ValidationDecorator", "Envolve todo IRequestHandler: registra duração e códigos de erro, roda o validator do FluentValidation.")
        Component(commands, "Handlers de command", "CreateUserCommandHandler", "Cria o agregado, verifica o e-mail, faz o commit via IUnitOfWork.")
        Component(queries, "Handlers de query", "GetAllUsers, GetUserById", "Leem apenas via IUserQueries e retornam UserDto.")
        Component(dispatcher, "DomainEventDispatcher", "IDomainEventDispatcher", "Encontra os IDomainEventHandler<T> de um evento.")
        Component(eventHandlers, "Handlers de eventos de domínio", "SendWelcomeEmailWhenUserCreated", "Reagem aos eventos entregues pelo outbox.")
    }

    Container_Boundary(domainLayer, "Domain") {
        Component(user, "Agregado User", "AggregateRoot, Result<T>, Errors", "Garante as invariantes em User.Create e dispara UserCreatedDomainEvent.")
    }

    Container_Boundary(infraLayer, "Infrastructure") {
        Component(writeSide, "Lado de escrita", "IUserRepository + IUnitOfWork (EF Core ou ADO.NET)", "Rastreia agregados; o SaveChanges grava eles e as linhas do outbox numa única transação.")
        Component(readSide, "Lado de leitura", "IUserQueries (EF Core AsNoTracking ou ADO.NET)", "Projeta direto para UserDto.")
        Component(instrumentation, "Instrumentação da persistência", "*InstrumentationDecorator", "Registra e mede toda chamada de persistência, qualquer que seja o provider.")
        Component(outbox, "Outbox", "OutboxBackgroundService + OutboxProcessor (EF Core)", "Faz polling, reserva lotes, roda os handlers, tenta de novo com backoff, dead letter.")
        Component(emailSender, "LoggingEmailSender", "IEmailSender", "Registra o e-mail no log em vez de enviá-lo.")
    }

    Rel(client, usersController, "Requisições HTTP", "JSON")
    Rel(usersController, pipeline, "Handle(command/query)")
    Rel(pipeline, commands, "Chama")
    Rel(pipeline, queries, "Chama")
    Rel(commands, user, "User.Create")
    Rel(commands, instrumentation, "IUserRepository, IUnitOfWork")
    Rel(queries, instrumentation, "IUserQueries")
    Rel(instrumentation, writeSide, "Delega para")
    Rel(instrumentation, readSide, "Delega para")
    Rel(writeSide, db, "INSERT Users + OutboxMessages")
    Rel(readSide, db, "SELECT")
    Rel(outbox, db, "UPDATE … OUTPUT (reserva), consumers")
    Rel(outbox, dispatcher, "GetInvocations(evento)")
    Rel(dispatcher, eventHandlers, "Resolve")
    Rel(eventHandlers, emailSender, "SendAsync")
    Rel(emailSender, email, "Planejado")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="2")
```

`errorHandling` não tem setas de propósito: são middleware e factories do ASP.NET Core que
respondem a qualquer exceção ou erro produzido pelo controller e pelo framework.

| Componente | Onde | Observações |
|---|---|---|
| UsersController | `src/Api/Controllers/UsersController.cs` | `[FromServices] IRequestHandler<TRequest, Result<T>>` por action |
| Respostas de erro | `src/Api/Common/Errors/` | `ErrorType` → 400/404/409/500; `RequestErrors` para erros de roteamento |
| Pipeline de requisições | `src/Application/Common/Decorators/`, `src/Application/DependencyInjection.cs` | Decorators montados no startup por varredura do assembly |
| Handlers de command | `src/Application/Users/Commands/` | Retornam `Result<UserDto>`, nunca entidades |
| Handlers de query | `src/Application/Users/Queries/` | Nunca usam `IUserRepository` (teste de arquitetura) |
| Despacho de eventos de domínio | `src/Application/Common/Events/`, `src/Application/Users/Events/` | Handlers precisam tolerar repetição (at-least-once) |
| Agregado User | `src/Domain/UserAggregate/` | Sem pacotes, sem conhecimento de persistência |
| Lados de escrita / leitura | `src/Infrastructure/Persistence/` (`Repositories`, `Queries`, `Ado/`) | `Persistence:Provider` = `Ado` ou `EfCore` |
| Instrumentação | `src/Infrastructure/Persistence/Decorators/`, `Instrumentation/` | Histograma `cqrsapi.persistence.operation.duration` |
| Outbox | `src/Infrastructure/Outbox/` | Sempre EF Core, reservas com `UPDLOCK, READPAST` |

## Visão dinâmica: `POST /api/users`

A requisição responde assim que o usuário e seu evento são gravados; o e-mail de boas-vindas
é enviado depois pelo outbox, em outro escopo.

```mermaid
C4Dynamic
    title Dinâmico: criação de um usuário

    Person(client, "Cliente da API")
    ContainerDb(db, "Banco de dados", "SQL Server")

    Container_Boundary(api, "Web API") {
        Component(controller, "UsersController")
        Component(pipeline, "LoggingDecorator → ValidationDecorator")
        Component(handler, "CreateUserCommandHandler")
        Component(user, "Agregado User")
        Component(uow, "IUserRepository + IUnitOfWork")
        Component(outbox, "OutboxProcessor")
        Component(welcome, "SendWelcomeEmailWhenUserCreated")
    }

    Rel(client, controller, "1. POST /api/users", "JSON")
    Rel(controller, pipeline, "2. Handle(CreateUserCommand)")
    Rel(pipeline, handler, "3. Entrada válida → Handle")
    Rel(handler, user, "4. User.Create dispara UserCreatedDomainEvent")
    Rel(handler, uow, "5. ExistsByEmail, Add, SaveChangesAsync")
    Rel(uow, db, "6. INSERT do usuário + linha do outbox, uma transação")
    Rel(controller, client, "7. 201 Created + Location")
    Rel(outbox, db, "8. Reserva as mensagens pendentes (a cada 2 s)")
    Rel(outbox, welcome, "9. Roda o handler, registra o consumer")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

Caminhos de falha: entrada inválida para no passo 3 (400 `User.Invalid*`); um e-mail duplicado
para no passo 5 ou, sob concorrência, no passo 6 via `UniqueConstraintViolationException` (409
`User.DuplicateEmail`, nada é gravado); um handler de evento que falha é tentado de novo com
backoff exponencial e vai para dead letter após 10 tentativas.

## Deployment: Docker Compose local

```mermaid
C4Deployment
    title Deployment: docker compose (desenvolvimento local)

    Deployment_Node(dev, "Máquina do desenvolvedor", "macOS / Linux / Windows, Docker") {
        Deployment_Node(compose, "Projeto Compose cqrs-api-example") {
            Deployment_Node(apiNode, "cqrs-api-example-api", "mcr.microsoft.com/dotnet/aspnet:10.0, sem root") {
                Container(api, "Web API", "ASP.NET Core, porta 8080", "ASPNETCORE_ENVIRONMENT=Development: aplica migrations, faz o seed, Swagger.")
            }
            Deployment_Node(dbNode, "cqrs-api-example-sql", "mcr.microsoft.com/mssql/server:2022-latest, linux/amd64") {
                ContainerDb(db, "CqrsApi", "SQL Server 2022, porta 1433", "Volume mssql-data.")
            }
        }
    }

    Rel(api, db, "Server=db,1433", "TDS")

    UpdateLayoutConfig($c4ShapeInRow="2", $c4BoundaryInRow="1")
```

As portas do host ficam ligadas só em `127.0.0.1`: a API na **5235** (→ 8080) e o SQL Server na
**14330** (→ 1433, `MSSQL_HOST_PORT`). A API espera o healthcheck do banco. Fora de Development
nenhuma migration é aplicada automaticamente.

## Nível 4: Código (seleção)

Só as partes que valem uma figura: o modelo de domínio e as abstrações de persistência que
pertencem à Application. O resto se lê melhor no próprio código.

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
    User ..> UserCreatedDomainEvent : dispara
    IUserRepository ..> User : rastreia
    IUserQueries ..> UserDto : projeta para

    IUserRepository <|.. UserRepository : EF Core
    IUserRepository <|.. AdoUserRepository : ADO.NET
    IUnitOfWork <|.. UnitOfWork : EF Core
    IUnitOfWork <|.. AdoUnitOfWork : ADO.NET
    IUserQueries <|.. UserQueries : EF Core
    IUserQueries <|.. AdoUserQueries : ADO.NET
```
