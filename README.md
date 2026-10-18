# RaceDay API (Part 2 - RESTful API Development)

The RaceDay API is the back end of the RaceDay system. It handles users, events, categories,
enrolments and results, and it enforces role-based access for **Organisers** and **Participants**.

- **Video demo (YouTube, unlisted):** [(https://youtu.be/UKxriwdSIQ8)]
- **Build status:** ![<img width="932" height="475" alt="image" src="https://github.com/user-attachments/assets/4acde1f3-26f3-4ba8-961f-378737323eea" /> 
]
(![Uploading image.png…]()
) 
## Fixed Part
I Updated my ERD from part 1 because it did not contain "Organiser" and "Participants" :[https://github.com/Onalenna2112/RaceDayApi2/blob/master/RaceDay_ERD%20(1).png

]
So that is the updated version of my ERD.

## Technology

ASP.NET Core 8 Web API (C#), Entity Framework Core 8 (Code-First), SQL Server (LocalDB),
JWT bearer authentication, Swagger (Swashbuckle), xUnit tests, GitHub Actions.

## Setup and run

You need: Visual Studio 2022 (with the ASP.NET workload), the .NET 8 SDK and SQL Server LocalDB
(installed with Visual Studio).

1. Clone the repository and open `RaceDayApi.sln`.
2. Check the connection string in `RaceDayApi/appsettings.json` (default: LocalDB, database `RaceDayDb`).
3. Create the database from the models (Code-First).
   In Visual Studio open **Tools > NuGet Package Manager > Package Manager Console**, set the
   default project to `RaceDayApi`, and run:
   ```
   Add-Migration InitialCreate
   Update-Database
   ```
   Or with the command line, from the solution folder:
   ```
   dotnet ef migrations add InitialCreate --project RaceDayApi
   dotnet ef database update --project RaceDayApi
   ```
   *(If you created the database with an older migration earlier, run `Drop-Database` first.)*
4. Press **F5** (or `dotnet run --project RaceDayApi`). Swagger opens in the browser at `/swagger`.

### Settings you can change (`appsettings.json`)

| Setting | Meaning |
|---|---|
| `Jwt:Key` | Secret used to sign login tokens (use a long random value; in real life keep it in user-secrets). |
| `Jwt:ExpiryMinutes` | How long a login token stays valid. |
| `Auth:OrganiserRegistrationCode` | Secret code required to register as an Organiser. |

## The two roles

| | Organiser | Participant |
|---|---|---|
| How to register | `POST /api/auth/register` with `role: "Organiser"` **and** the secret `organiserCode` | `POST /api/auth/register` with `role: "Participant"` |
| Events | Create, update and delete **own** events; view all events | View events |
| Categories | Create, update and delete categories of **own** events | View categories |
| Enrolments | View all enrolments for **own** events; remove an enrolment from own event | Enrol in an event by choosing a category; view and cancel **own** enrolments |
| Results | Capture finish time and position; view all results of **own** events | View **own** results |
| Profile | View and update own profile | View and update own profile |

An Organiser cannot use Participant functions and a Participant cannot use Organiser functions
(the API answers **403 Forbidden**). Requests without a valid token get **401 Unauthorized**.
Only `register` and `login` are public.

Why the organiser code? Anyone can sign up on the public API, so an Organiser account must not be
available to everybody. The code is known only to the system owner.

## How authentication works

1. `POST /api/auth/register` creates the account. The password is hashed with ASP.NET Core's
   `PasswordHasher` and only the hash is stored.
2. `POST /api/auth/login` checks the password and returns a signed **JWT token** that contains the
   user's id and role.
3. In Swagger click **Authorize** and paste the token. Every later request carries the token, and
   `[Authorize]` / `[Authorize(Roles = "...")]` attributes decide what the user may do.

## Endpoints

| Method | Route | Who |
|---|---|---|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |
| GET, PUT | `/api/users/{id}` | Logged in (own profile only) |
| GET | `/api/events`, `/api/events/{id}` | Both roles |
| POST | `/api/events` | Organiser |
| PUT, DELETE | `/api/events/{id}` | Organiser (own event) |
| GET | `/api/events/{eventId}/categories` | Both roles |
| POST | `/api/events/{eventId}/categories` | Organiser (own event) |
| PUT, DELETE | `/api/categories/{id}` | Organiser (own event) |
| POST | `/api/events/{id}/enrolments` | Participant |
| GET | `/api/events/{id}/enrolments` | Organiser (own event) |
| GET | `/api/participants/{id}/enrolments` | Participant (own) |
| DELETE | `/api/enrolments/{id}` | Participant (own) or Organiser (own event) |
| POST | `/api/enrolments/{enrolmentId}/results` | Organiser (own event) |
| GET | `/api/events/{eventId}/results` | Organiser (own event) |
| GET | `/api/participants/{id}/results` | Participant (own) |

Every endpoint shows its description, expected data and possible responses in Swagger.

## Tests

```
dotnet test
```

- **Unit tests** (`AuthControllerTests`, `EventsControllerTests`) call the controllers directly.
- **API tests** (`AuthApiTests`, `EventApiTests`, `EnrolmentApiTests`, `ResultApiTests`) start the real
  API in memory (real JWT authentication and role checks, in-memory database) and check
  both success and failure cases: registration and login, authenticated vs. unauthenticated requests,
  Organiser-only and Participant-only actions, enrolment records, and results.

## CI/CD

`.github/workflows/dotnet.yml` builds the solution and runs all tests on every push.

![CI build](docs/green-build.png)

## Project structure

```
RaceDayApi/                API project
  Controllers/             Auth, Users, Events, Categories, Enrollment, Results
  Models/                  Entities, request DTOs, response DTOs, Roles
  Services/                TokenService (JWT), claim helpers
  Data/                    RaceDayDbContext
RaceDayApi.Tests/          xUnit tests
.github/workflows/         GitHub Actions workflow
```
