using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;
using Ratatosk.Domain.Identity.Events;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class SeedAdminCommandHandlerTests
{
    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "admin-password";

    // The password hasher and token issuer are true externals; everything else is the real wiring.
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenIssuer> _tokenIssuer = new();
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _passwordHasher
            .Setup(x => x.Hash(It.IsAny<Password>()))
            .Returns((Password p) => PasswordHash.Create($"hashed:{p.Value}").Value!);
        _tokenIssuer
            .Setup(x => x.IssueToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<string>.Success("access_token"));

        _host = await TestHost.StartAsync(services =>
        {
            services.AddSingleton(_passwordHasher.Object);
            services.AddSingleton(_tokenIssuer.Object);
        });
    }

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task SeedAdmin_CreatesAndProjectsAnAdminUser()
    {
        var result = await _host.SendAsync(new SeedAdminCommand(AdminEmail, AdminPassword));

        Assert.IsTrue(result.IsSuccess);
        var userAuth = await _host.Users.GetByEmailAsync(AdminEmail, CancellationToken.None);
        Assert.AreEqual(UserRole.Admin.Name, userAuth!.Role);
        Assert.IsInstanceOfType<UserCreated>((await _host.StreamAsync<User>(userAuth.Id)).Single());
        Assert.IsEmpty(_host.RefreshTokens.All);
        _tokenIssuer.Verify(
            x => x.IssueToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task SeedAdmin_Twice_SucceedsAndCreatesOneUser()
    {
        (await _host.SendAsync(new SeedAdminCommand(AdminEmail, AdminPassword))).EnsureSuccess();

        var result = await _host.SendAsync(new SeedAdminCommand(AdminEmail, "another-password"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, (await _host.Users.GetAllAsync(CancellationToken.None)).TotalItems);
        var userAuth = await _host.Users.GetByEmailAsync(AdminEmail, CancellationToken.None);
        Assert.AreEqual($"hashed:{AdminPassword}", userAuth!.Hash);
        Assert.HasCount(1, await _host.StreamAsync<User>(userAuth.Id));
    }

    [TestMethod]
    public async Task SeedAdmin_ForAnExistingUser_LeavesItsRoleAndHashAsTheyAre()
    {
        (await _host.SendAsync(new SignUpCommand(AdminEmail, "user-password"))).EnsureSuccess();

        var result = await _host.SendAsync(new SeedAdminCommand(AdminEmail, AdminPassword));

        Assert.IsTrue(result.IsSuccess);
        var userAuth = await _host.Users.GetByEmailAsync(AdminEmail, CancellationToken.None);
        Assert.AreEqual(UserRole.User.Name, userAuth!.Role);
        Assert.AreEqual("hashed:user-password", userAuth.Hash);
        Assert.HasCount(1, await _host.StreamAsync<User>(userAuth.Id));
    }

    [TestMethod]
    public async Task SeedAdmin_WithAnEmptyPassword_FailsAndCreatesNoUser()
    {
        var result = await _host.SendAsync(new SeedAdminCommand(AdminEmail, ""));

        Assert.IsTrue(result.IsFailure);
        Assert.IsNull(await _host.Users.GetByEmailAsync(AdminEmail, CancellationToken.None));
    }

    [TestMethod]
    public async Task SeedAdmin_WithAnInvalidEmail_FailsAndCreatesNoUser()
    {
        var result = await _host.SendAsync(new SeedAdminCommand("not-an-email", AdminPassword));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(0, (await _host.Users.GetAllAsync(CancellationToken.None)).TotalItems);
    }
}
