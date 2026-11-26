# Verification plan

The automated suite targets the real ASP.NET Core host and SQL Server provider. It must use a disposable database name derived from `LOUND_TEST_CONNECTION`, so it exercises migrations, SQL constraints, and concurrent approval instead of replacing SQL Server with an in-memory provider.

## Database safety and setup

- Set `LOUND_TEST_CONNECTION` to a SQL Server connection string with permission to create and drop databases on a local development or isolated test instance. Never point it at a production database.
- The fixture must replace the configured catalog with a randomly generated `LoundTests_<guid>` name, apply the API migrations to that catalog, and drop only that generated database on disposal.
- If the variable is missing, tests must report a clear blocked/skipped result. If SQL Server cannot be reached or database creation is denied, report the exact SQL failure without printing credentials from the connection string.
- Use unique synthetic addresses and generated secrets in every test run. Do not use the poster's attribution or contact details.

## API coverage

1. Register two standard users and seed one administrator. Confirm a standard user receives `403` on every admin route while the administrator can access it.
2. Submit a pending report. Confirm it is absent from shared search and inaccessible to another user by detail ID, but appears in the submitter's own reports. Verify it as admin and confirm it appears in search and details.
3. Submit a found report, verify it, and submit competing claims from the two users. Confirm evidence appears only in the matching claimant's claim list and the admin claim review, never in another user's claim list or any item response.
4. Approve competing claims concurrently. Exactly one approval may return success; the item must be `Claimed`, exactly one claim must be `Approved`, and the competing pending claim must be `Rejected`. Rejected claims alone must leave the item `Found`.
5. Record handover. Confirm the approved claim becomes `Returned`, records a return time, the item is `Returned`, claimant history shows the return, and admin dashboard/date-filtered return counts include it.
6. Reject and remove reports. Confirm both states remain visible to admins and the owner, while removed items disappear from shared search.
7. Deactivate a user, then remove another from the active directory. Confirm existing bearer tokens stop working immediately, reactivation/restore controls follow the separate state flags, and linked history remains intact.
8. Restart the test host while retaining the database. Confirm user profiles, report states, claims, and returns persist.
9. Submit invalid report and claim input and reversed date ranges. Confirm validation responses are safe and no partial rows are created.

## Desktop acceptance walkthrough

Use only synthetic records and the actual Windows Forms application. Validate register, sign-in/out, profile edit, report creation, pending invisibility, search/filter/details after admin verification, competing claims and evidence visibility, one winning approval, return state on claimant/admin views, reject/remove behavior, user deactivation, invalid form feedback, and persistence after API/client restart. Inspect keyboard navigation and display scaling on the Windows desktop. Record every blocked step with its environment cause.
