Amrod Order Management System

This is my full-stack Order Management solution for the Amrod technical assessment.

The application manages customers and orders for SADC countries. I built the backend using .NET 8, Entity Framework Core and SQL Server, the frontend using React and TypeScript, and RabbitMQ is used for asynchronous order processing.

The project also includes validation, authentication and authorization, pagination, filtering and sorting, idempotent status updates, optimistic concurrency, health checks, metrics, automated tests, Docker and CI/CD.

Technology Stack

Backend
.NET 8
ASP.NET Core Web API
Entity Framework Core
SQL Server
FluentValidation
JWT Bearer Authentication
Swagger / OpenAPI
RabbitMQ
Frontend
React
TypeScript
Vite
React Testing Library
Vitest
Framer Motion
Infrastructure
Docker
Docker Compose
SQL Server 2022
RabbitMQ
Nginx
GitHub Actions

Architecture
The solution has three main application components:

                        React Frontend
                            |
                            v
                        ASP.NET Core API
                            |
                            +-------------------+
                            |                   |
                            v                   v
                        SQL Server            RabbitMQ
                                                |
                                                v
                                            Worker Service
                                                |
                                                v
                                            SQL Server

API
The ASP.NET Core API is responsible for:
Customer management
Order creation and retrieval
Filtering, sorting and pagination
Order lifecycle transitions
SADC country and currency validation
Server-side order total calculation
JWT authentication and permission-based authorization
Idempotent status updates
ETag conditional requests
RabbitMQ event publishing
Health/readiness checks
Basic request metrics

Worker
The Worker Service consumes OrderCreated events from RabbitMQ and simulates downstream order processing by moving the order to Fulfilled.
For message reliability I used durable queues, persistent messages, manual acknowledgements and a prefetch count. I also added a retry queue with a delay and a dead-letter queue. A failed message is retried up to three times before it is sent to the dead-letter queue.
The worker also checks whether an order is already fulfilled before processing it again. This makes the consumer idempotent when the same message is delivered more than once.

Order Lifecycle
The supported order flow is:

                        Pending
                        |
                        +----> Paid ----> Fulfilled
                        |         |
                        |         +----> Cancelled
                        |
                        +----> Cancelled

Fulfilled and Cancelled are terminal states. The API rejects invalid transitions instead of allowing an order to move to any status.
SADC Country and Currency Validation
Orders must use a supported SADC country and a valid currency for that country. The validation is handled on the backend using FluentValidation, so it cannot be bypassed by only changing the frontend.

Examples include:
Country
Currency
South Africa
ZAR
Botswana
BWP
Namibia
NAD / ZAR
Eswatini
SZL / ZAR
Lesotho
LSL / ZAR
Zambia
ZMW
Zimbabwe
ZWL / USD
Mozambique
MZN
Malawi
MWK
Tanzania
TZS

For the Common Monetary Area (CMA), Namibia, Lesotho and Eswatini can also use ZAR in this assessment. I therefore allow both the local currency and ZAR for those countries instead of assuming that every country has only one accepted currency.
The complete country/currency mapping is implemented in the order request validator.

Authentication and Authorization
For the assessment I implemented a mock JWT issuer so that the authorization flow can be demonstrated without requiring a real Microsoft Entra tenant.

The permissions are:
Orders.Read
Orders.Write
Orders.Admin
Orders.Admin is allowed to perform both read and write operations.

Generate a development token
POST /api/auth/token

                            Example request:

                            {
                            "permission": "Orders.Admin"
                            }

Use the returned token as:
Authorization: Bearer <token>
The mock token endpoint is only for development and the assessment. In a production environment I would validate tokens from Microsoft Entra ID or another trusted identity provider rather than issuing tokens from the application itself.

API Endpoints
Customers
POST /api/customers
GET  /api/customers/{id}
GET  /api/customers
The customer list supports search and pagination.

Orders
POST /api/orders
GET  /api/orders/{id}
GET  /api/orders
PUT  /api/orders/{id}/status
The order list supports:

Filtering by customer
Filtering by status
Sorting by created date or total amount
Ascending/descending sorting
Pagination
The maximum page size is 100.

An order can contain multiple line items. The frontend allows line items to be added or removed before the order is submitted, while the final total is calculated again on the server.

Idempotent Status Updates
A status update requires an idempotency key:
Idempotency-Key: <unique-key>
The API stores the last status idempotency key on the order. If the same request is repeated with the same key, the status change is not applied twice.
I also use SQL Server rowversion through EF Core for optimistic concurrency. If another request updates the same order before the current save completes, the API handles the concurrency exception and returns a conflict response.

ETag Support
GET /api/orders/{id} returns an ETag based on the order row version.
A client can send the value back using:
If-None-Match: "<etag>"
If the order has not changed, the API returns:
304 Not Modified
This avoids returning the same order payload again when the client already has the current version.

Health and Metrics
The API exposes:
GET /healthz
GET /readiness
GET /metrics
/healthz confirms that the API is running.
/readiness also checks SQL Server connectivity, so it can show whether the application is ready to handle requests that depend on the database.

/metrics exposes simple in-memory request metrics including the total request count, average request duration and counts grouped by HTTP status code. For a larger production system I would normally export metrics to a dedicated monitoring platform instead of keeping them only in application memory.

Database
I used Entity Framework Core Code First for database management.
The main entities are:
Customer
Order
OrderLineItem
Orders use decimal(18,2) for money values and SQL Server rowversion for concurrency.
I added the composite index:
IX_Orders_CustomerId_Status_CreatedAt
This supports the common order filtering fields. Read-only EF Core queries use AsNoTracking() to avoid unnecessary change tracking.

Database Migrations
The EF Core migrations are stored in the API project.
Apply the migrations with:
dotnet ef database update `
  --project .\src\Api\Amrod.OrderManagement.Api.csproj `
  --startup-project .\src\Api\Amrod.OrderManagement.Api.csproj

Generate an idempotent SQL deployment script with:
dotnet ef migrations script --idempotent `
  --project .\src\Api\Amrod.OrderManagement.Api.csproj `
  --startup-project .\src\Api\Amrod.OrderManagement.Api.csproj `
  --output .\database\migration.sql

A generated migration script is included at:
database/migration.sql
Migration deployment and rollback approach

For production I would not automatically run a risky schema change at the same time as replacing every application instance. I would first generate and review the migration SQL, back up the database, and apply backward-compatible database changes before deploying code that depends on them. For example, a new column would normally be added as nullable or with a safe default first. The application can then be deployed gradually, data can be backfilled if required, and stricter constraints can be added in a later migration. This approach allows the old and new application versions to run against the schema during a rolling deployment and reduces downtime.

For rollback, I would normally roll the application back first while keeping compatible additive database changes in place. If the database change itself has to be reversed, I would use a tested rollback migration/script and restore from the backup if data was affected. Destructive changes such as dropping or renaming a column should be separated from the initial deployment and only performed after the old application version is no longer using that schema. I would also test the forward and rollback scripts in a non-production environment before applying them to production.

Seed Data
The application seeds demonstration data when the database does not already contain the seed marker.
The seed contains:
100 customers
1,000 orders
Multiple SADC countries
Multiple order statuses
The marker prevents the same seed dataset from being inserted every time the application starts.
Running with Docker

Prerequisites
Install Docker Desktop with Docker Compose support.
Create a .env file in the repository root using .env.example as the template:
SQL_SA_PASSWORD=YourStrongSqlServerPasswordHere
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
JWT_KEY=ReplaceWithAStrongJwtSigningKeyAtLeast32CharactersLong
The .env file contains local secrets and should not be committed.

Start the complete application
From the repository root:
docker compose up --build
Or run it in the background:
docker compose up -d --build
Local addresses
Service
Address
Web application
http://localhost:5173

API
http://localhost:5254
Swagger
http://localhost:5254/swagger

RabbitMQ Management
http://localhost:15672

SQL Server
localhost:1433

Check the containers with:
docker compose ps

Stop the environment with:
docker compose down

Running the Backend Locally
Restore dependencies:

dotnet restore .\Amrod.OrderManagement.slnx
Build:
dotnet build .\Amrod.OrderManagement.slnx

For local development outside Docker, I use environment variables or .NET User Secrets for sensitive values rather than putting secrets into committed appsettings.json files.

Running the Frontend Locally
cd src\frontend
npm install
npm run dev

The development server runs at:
http://localhost:5173

Create a production build with:
npm run build
Frontend Functionality
The frontend includes:
Customer list with search and pagination
Customer creation
Order list with status filtering
Sorting by created date and total amount
Pagination
Order creation
Multiple line-item creation
Order details with individual line items and totals
Order lifecycle actions
Loading, success, error and empty states
The API client is written in TypeScript and uses shared request/response types for the frontend API calls.

Automated Tests
Backend
Run:
dotnet test .\Amrod.OrderManagement.slnx
The backend tests cover areas such as:
Order total calculation
Order validation
SADC currency validation
Authentication and authorization
Customer creation and retrieval
Order creation
RabbitMQ event publication
Order lifecycle transitions
Status update idempotency
Invalid transitions
Required idempotency keys
Health checks
Frontend
cd src\frontend
npm test -- --run

The frontend tests cover customer and order behaviour such as loading data, searching, pagination, status filtering and order status actions.
The production frontend compilation can also be checked with:
npm run build

Postman
The Postman collection is located at:
postman/Amrod.OrderManagement.postman_collection.json
It contains requests for the main API flows including:
JWT token generation
Customer creation and retrieval
Customer search
Order creation and retrieval
Filtering and sorting
Status transitions
Idempotency
ETag conditional requests
Authorization checks
Health/readiness
The default base URL is http://localhost:5254.
Correlation IDs and Logging
The API uses correlation IDs so that an HTTP request can be followed into asynchronous processing.
A client can provide:
X-Correlation-ID: <value>
If it is not supplied, the API creates an identifier. The correlation ID is also added to the RabbitMQ message and used by the worker logging.
I use ILogger for application logging and include the correlation context where it is useful.

RabbitMQ Reliability
The RabbitMQ implementation uses:
A durable exchange
Durable queues
Persistent messages
Manual acknowledgements
Prefetch control
Retry queue
Retry count header
Retry delay
Dead-letter queue

When processing fails, the worker republishes the message to the retry queue with an incremented retry count. After the retry limit is reached, it is sent to the dead-letter queue instead of being retried forever.
Production improvement: Outbox Pattern

One limitation in the current implementation is that the order is committed to SQL Server before the OrderCreated event is published to RabbitMQ. There is therefore a small failure window where the database save could succeed but message publishing could fail.

In production I would improve this with the Transactional Outbox Pattern. The order and an outbox event would be saved in the same SQL transaction. A background publisher would then publish unsent outbox events to RabbitMQ and mark them as dispatched. This removes the direct database/message-broker dual-write problem and makes event publishing more reliable.

CI/CD
The GitHub Actions workflow is located at:
.github/workflows/ci.yml
The pipeline is intended to check:
Backend dependency restore
Backend build
Backend automated tests
Frontend dependency installation
Frontend automated tests
Frontend production build
EF Core migrations against an empty SQL Server database
Generation of an idempotent migration SQL artifact

Security
Secrets are not meant to be stored in source control. Local Docker secrets are supplied through .env, while .env.example only contains placeholders.

For local backend development I can use .NET User Secrets or environment variables. In CI/CD and production I would use the environment's secret management solution.

The JWT issuer included in this project is only a development implementation for the assessment. A production version should use Microsoft Entra ID and validate tokens issued by the trusted identity provider.

Performance and Scalability
Some of the decisions I made for performance and scalability are:
Server-side pagination instead of returning all records
Maximum page size of 100
AsNoTracking() for read-only EF Core queries
Composite indexing for common order queries
Async database and messaging operations
RabbitMQ for asynchronous downstream processing
Worker prefetch control
A stateless API that can be scaled horizontally
ETag support to reduce unnecessary responses

For a larger production environment I would also consider distributed caching where it makes sense, multiple API and worker instances, database connection resiliency, RabbitMQ clustering, centralized logs/metrics dashboards and the Outbox Pattern.

Assumptions
The JWT token endpoint is a mock/development identity implementation for this assessment.
RabbitMQ simulates downstream order fulfilment.
SQL Server is the authoritative persistence store.
Docker Compose is used to make local assessment setup easier.
Production credentials and secrets must be supplied externally.

Written Questions
My answers to the technical and SQL questions are in:
ANSWERS.md