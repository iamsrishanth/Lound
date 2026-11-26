# Verification results

Date: 2026-11-26

## Automated integration suite

- `dotnet restore tests/Lound.Api.IntegrationTests/Lound.Api.IntegrationTests.csproj` succeeded after the API pinned a patched `System.Security.Cryptography.Pkcs` version.
- `dotnet build .\Lound.sln --configuration Release --no-restore` succeeded with zero warnings and zero errors.
- `dotnet test .\tests\Lound.Api.IntegrationTests\Lound.Api.IntegrationTests.csproj --configuration Release --no-restore --verbosity minimal` ran against SQL Server LocalDB and passed all 6 tests with no skips.
- The suite covers role enforcement, pending report visibility, private claim evidence, report rejection and removal, concurrent approvals, claim rejection, returns and statistics, account status, validation, and persistence after host restart.

## Windows desktop smoke

- The Release WinForms client launched and its refreshed sign-in screen was inspected at 120 DPI (125% effective Windows scale). The app manifest declares PerMonitorV2, and forms use `AutoScaleMode.Font`.
- A synthetic acceptance walkthrough used two standard users and an administrator. Users submitted reports and claims; the administrator verified reports, inspected private evidence, approved one competing claim, and rejected the other.
- Handover and return statistics were verified through the SQL-backed integration suite. The post-redesign admin action bar has not yet had a visual walkthrough; claim actions now follow selection and claim status, and the selected claim is retained after a state change.
- The desktop visual check covered the sign-in screen only after the redesign. The updated four-column search panel and admin tables were confirmed by successful Release compilation.
