# Lound

Lound is a Windows lost and found management system. The Windows Forms client targets .NET Framework 4.8. An ASP.NET Core API on .NET 10 LTS handles accounts, permissions, reports, claims, and SQL Server data.

## Prerequisites

- Windows 10 or Windows 11.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The project builds with the SDK command line or Visual Studio with .NET 10 support.
- .NET Framework 4.8 runtime. It is already included with supported Windows versions. The desktop project restores the .NET Framework 4.8 reference assemblies from NuGet; a Visual Studio installation can instead use the [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48).
- SQL Server Express LocalDB for local development, or another SQL Server instance. LocalDB runs as the current Windows user and uses Windows authentication. See Microsoft's [LocalDB installation and connection guide](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb).
- Internet access on the first build to restore NuGet packages.

The solution contains the API, Windows Forms client, and SQL Server integration tests. No SQL credentials are stored in the desktop client or checked into this repository.

## Restore and build

Run these commands from the repository root in PowerShell:

```powershell
dotnet restore .\Lound.sln
dotnet build .\Lound.sln --configuration Release --no-restore
```

The desktop output is `src\Lound.Desktop\bin\Release\net48\Lound.Desktop.exe`.

## SQL Server configuration and migrations

The development default is the LocalDB instance `(localdb)\MSSQLLocalDB` with database name `LoundDev`. Override the server connection on the API process with the `ConnectionStrings__DefaultConnection` environment variable. For example:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=LoundDev;Integrated Security=true;MultipleActiveResultSets=true;TrustServerCertificate=true'
```

For a SQL Server instance, replace that value with its server and database. Keep any SQL username or password in a server side environment variable or secret store. Never place production credentials in the client or a committed settings file.

Apply the versioned EF Core migrations and create required roles:

```powershell
dotnet run --project .\src\Lound.Api\Lound.Api.csproj -- --migrate
```

The API does not automatically migrate a production database when it starts. Back up an existing database before applying a new migration.

## First administrator setup

Run this command after SQL Server is available. It applies migrations if needed, asks for an email and display name, and reads the password without echoing it to the console. It refuses to create a second first administrator.

```powershell
dotnet run --project .\src\Lound.Api\Lound.Api.csproj -- --setup-admin
```

New users who register in the desktop client receive the standard `User` role. The setup command is the only supported first administrator bootstrap path.

## Configure a signing key

In Development, the API can start without a configured signing key and creates a temporary key for that process. Existing tokens expire when that process restarts. To keep sessions valid across restarts, set a stable server side `Jwt__SigningKey` containing at least 32 random bytes. Outside Development, the API requires this setting.

For a temporary local PowerShell session, generate a key with the system cryptographic random number generator:

```powershell
$keyBytes = New-Object byte[] 64
$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$random.GetBytes($keyBytes)
$env:Jwt__SigningKey = [Convert]::ToBase64String($keyBytes)
$random.Dispose()
```

For longer lived use, store the key in the API host's secret manager or environment configuration. Do not share it with the client.

## Run the API and desktop client

Start the API in one PowerShell window. The development launch profile listens at `http://localhost:5080`.

```powershell
dotnet run --project .\src\Lound.Api\Lound.Api.csproj
```

In another window, launch the desktop client:

```powershell
dotnet build .\src\Lound.Desktop\Lound.Desktop.csproj --configuration Release
Start-Process .\src\Lound.Desktop\bin\Release\net48\Lound.Desktop.exe
```

The sign in screen starts with `http://localhost:5080`. Use HTTPS for API servers outside the local machine. The client keeps its access token in memory and clears it at sign out.

## Tests

The integration tests use the real SQL Server provider and migrations. Set `LOUND_TEST_CONNECTION` to a SQL Server connection string with permission to create and drop databases on a dedicated test instance. Each test run uses a generated `LoundTests_<guid>` database and removes only that database during cleanup.

Example using LocalDB:

```powershell
$env:LOUND_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=true;TrustServerCertificate=true'
dotnet test .\tests\Lound.Api.IntegrationTests\Lound.Api.IntegrationTests.csproj --configuration Release
```

The suite covers admin role enforcement, pending report visibility, private claim evidence, concurrent approvals, returns, rejection and removal history, account deactivation and directory removal, invalid input, and persistence after an API host restart. If `LOUND_TEST_CONNECTION` is unset, the SQL integration tests are marked skipped with a reason. Do not point this variable at a production database.

## Implemented behavior

- Reports have independent moderation and item statuses. Only verified reports appear in shared search.
- Users can view all of their own reports and claims. Claim evidence is returned only to the claimant and administrators.
- Claim approval locks the item within a SQL transaction, rejects competing pending claims, and is protected by a filtered unique index.
- Deactivated and directory removed accounts lose access immediately while linked history remains in the database.
- Admin dashboards and date filtered summaries read live database records.

## Verification record

The current development machine has the .NET 10 SDK and .NET Framework 4.8 runtime. A .NET Framework reference assemblies package is restored by the desktop project. Visual Studio, standalone MSBuild, the .NET Framework 4.8 Developer Pack, and SQL Server LocalDB were not installed on the machine.

`dotnet build .\Lound.sln --configuration Release` passed with zero warnings and zero errors. `dotnet list .\Lound.sln package --vulnerable --include-transitive` found no vulnerable packages. The integration project built and discovered six tests; with `LOUND_TEST_CONNECTION` unset, all six were skipped and none failed or passed.

The API launched and `GET /health` returned `{"status":"ok"}`. The Windows Forms client launched at 120 DPI (125% effective scale). The final login title and subtitle fit, Tab navigation reached email, password, sign in, and registration, and an empty sign in showed inline validation. No credentials were entered and no accounts were created in that UI smoke.

The LocalDB installer was downloaded from Microsoft and signed by Microsoft, but installation stopped with Windows Installer exit code `1603`: the current Windows account is not in the administrator group. SQL backed workflows and the two user plus administrator walkthrough cannot be exercised on this machine until LocalDB or another SQL Server instance is made available. See [VERIFICATION_RESULTS.md](tests/VERIFICATION_RESULTS.md) for the full check record.
