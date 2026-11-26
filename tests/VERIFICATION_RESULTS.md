# Verification results

Date: 2026-11-26

## Automated integration suite

- `dotnet restore tests/Lound.Api.IntegrationTests/Lound.Api.IntegrationTests.csproj` succeeded after the API pinned a patched `System.Security.Cryptography.Pkcs` version.
- `dotnet test tests/Lound.Api.IntegrationTests/Lound.Api.IntegrationTests.csproj --no-restore --verbosity minimal` and the same command with `--configuration Release` both compiled the .NET 10 API and test assembly, discovered 6 integration tests, and reported 0 failures, 0 passes, and 6 skips.
- The tests were skipped because `LOUND_TEST_CONNECTION` was unset. They cover role enforcement, pending report visibility, private claim evidence, report rejection/removal, concurrent approvals, claim rejection, returns/statistics, account status, validation, and persistence after host restart.
- SQL-backed behavior remains unverified in this environment. SQL Server LocalDB installation failed with MSI exit 1603 because setup requires administrator-group membership; no SQL Server service or alternate SQL endpoint is available. Set `LOUND_TEST_CONNECTION` to an isolated SQL Server instance to execute the integration tests.

## Windows desktop smoke

- Launched the Release WinForms client and inspected the sign-in screen at 120 DPI (125% effective Windows scale, from the current desktop DPI registry value). The app manifest declares PerMonitorV2, and forms use `AutoScaleMode.Font`.
- Confirmed the final sign-in title and subtitle fit without clipping at the observed scale.
- Confirmed Tab navigation proceeds through email, password, sign-in, and account-registration link. Enter on an empty sign-in form displayed inline validation on the first build.
- No credentials were entered and no account was created during this UI smoke.
- The full acceptance walkthrough with two users and an administrator was blocked because the API requires SQL Server and LocalDB cannot be installed without administrator rights.
