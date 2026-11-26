using System.ComponentModel.DataAnnotations;
using System.Text;
using Lound.Api.Data;
using Lound.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lound.Api.Security;

public static class DatabaseBootstrap
{
    public static async Task MigrateAndSeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        var roles = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { RoleNames.User, RoleNames.Admin })
        {
            if (await roles.RoleExistsAsync(roleName)) continue;
            var result = await roles.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
                throw new InvalidOperationException("Unable to initialize application roles: " +
                    string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    public static async Task SetupFirstAdminAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await DatabaseBootstrap.MigrateAndSeedRolesAsync(services, cancellationToken);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        // Serialize first-admin setup across concurrent invocations on SQL Server.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT [Id] FROM [dbo].[AspNetRoles] WITH (UPDLOCK, HOLDLOCK) WHERE [NormalizedName] = N'ADMIN'",
            cancellationToken);
        if (await users.GetUsersInRoleAsync(RoleNames.Admin) is { Count: > 0 })
        {
            Console.Error.WriteLine("An administrator already exists. First-admin setup was not changed.");
            return;
        }

        Console.Write("Administrator email: ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            Console.Error.WriteLine("A valid email address is required.");
            return;
        }
        Console.Write("Display name: ");
        var displayName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
        {
            Console.Error.WriteLine("Display name must contain 1 to 100 characters.");
            return;
        }

        var password = ReadHidden("Password: ");
        var confirmation = ReadHidden("Confirm password: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("The passwords did not match.");
            return;
        }

        var user = new ApplicationUser { Email = email, UserName = email, DisplayName = displayName };
        var create = await users.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            foreach (var error in create.Errors) Console.Error.WriteLine(error.Description);
            return;
        }
        var addRole = await users.AddToRoleAsync(user, RoleNames.Admin);
        if (!addRole.Succeeded)
        {
            await users.DeleteAsync(user);
            foreach (var error in addRole.Errors) Console.Error.WriteLine(error.Description);
            return;
        }

        await transaction.CommitAsync(cancellationToken);
        Console.WriteLine("First administrator created.");
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0) buffer.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
        }
        Console.WriteLine();
        return buffer.ToString();
    }
}
