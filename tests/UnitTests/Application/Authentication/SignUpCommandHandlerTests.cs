using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;
using Ratatosk.Domain.Identity.Events;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class SignUpCommandHandlerTests
{
    private const string ValidEmail = "user@example.com";
    private const string ValidPassword = "password";

    // The password hasher and token issuer are true externals; everything else is the real wiring.
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenIssuer> _tokenIssuer = new();
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _passwordHasher.Setup(x => x.Hash(It.IsAny<Password>())).Returns(PasswordHash.Create("hashed").Value!);
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
    public async Task SignUp_CreatesAndProjectsTheUser_AndIssuesATokenPair()
    {
        var result = await _host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("access_token", result.Value!.AccessToken);

        var user = await _host.Users.GetByEmailAsync(ValidEmail, CancellationToken.None);
        Assert.AreEqual(UserRole.User.Name, user!.Role);
        Assert.AreEqual("hashed", user.Hash);

        var refreshToken = await _host.RefreshTokens.GetByTokenAsync(result.Value.RefreshToken);
        Assert.AreEqual(ValidEmail, refreshToken!.Email);
    }

    [TestMethod]
    public async Task SignUp_StoresAndIssuesTheUsersStreamId()
    {
        (await _host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword))).EnsureSuccess();

        var userAuth = await _host.Users.GetByEmailAsync(ValidEmail, CancellationToken.None);
        Assert.AreNotEqual(Guid.Empty, userAuth!.Id);
        Assert.IsInstanceOfType<UserCreated>((await _host.StreamAsync<User>(userAuth.Id)).Single());
        _tokenIssuer.Verify(x => x.IssueToken(userAuth.Id, ValidEmail, UserRole.User.Name), Times.Once);
    }

    [TestMethod]
    public async Task SignUp_ThenLoadingTheUser_KeepsTheIdItWasCreatedWith()
    {
        (await _host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword))).EnsureSuccess();
        var userAuth = await _host.Users.GetByEmailAsync(ValidEmail, CancellationToken.None);

        var user = await _host.LoadAsync<User>(userAuth!.Id);

        Assert.AreEqual(userAuth.Id, user.Id);
        Assert.AreEqual(ValidEmail, user.Email.Value);
    }

    [TestMethod]
    public async Task SignUp_WithAnEmailAlreadyInUse_FailsAndCreatesNoSecondUser()
    {
        (await _host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword))).EnsureSuccess();

        var result = await _host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.AccountAlreadyExists.Message, result.Error);
        Assert.AreEqual(1, (await _host.Users.GetAllAsync(CancellationToken.None)).TotalItems);
        Assert.HasCount(1, _host.RefreshTokens.All);
    }

    [TestMethod]
    public async Task SignUp_WithAnEmptyPassword_FailsAndCreatesNoUser()
    {
        var result = await _host.SendAsync(new SignUpCommand(ValidEmail, ""));

        Assert.IsTrue(result.IsFailure);
        Assert.IsNull(await _host.Users.GetByEmailAsync(ValidEmail, CancellationToken.None));
    }

    [TestMethod]
    public async Task SignUp_WhenNoTokenCanBeIssued_FailsWithoutStoringARefreshToken()
    {
        _tokenIssuer
            .Setup(x => x.IssueToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<string>.Failure("signing error"));

        var result = await _host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidToken.Message, result.Error);
        Assert.IsEmpty(_host.RefreshTokens.All);
    }

    [TestMethod]
    public async Task SignUp_WhenTheUserStoreFails_ReturnsTheFailure()
    {
        using var host = await TestHost.StartAsync(services =>
        {
            services.AddSingleton(_passwordHasher.Object);
            services.AddSingleton(_tokenIssuer.Object);
            services.AddSingleton<IUserAuthRepository>(new UnavailableUserStore());
        });

        var result = await host.SendAsync(new SignUpCommand(ValidEmail, ValidPassword));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("database unavailable", result.Error);
    }

    private sealed class UnavailableUserStore : IUserAuthRepository
    {
        private static Exception Unavailable => new InvalidOperationException("database unavailable");

        public Task<Pagination<UserAuth>> GetAllAsync(CancellationToken cancellationToken) =>
            throw Unavailable;

        public Task<Pagination<UserAuth>> GetAllByRole(
            string role,
            int page = 1,
            int pageSize = 25,
            CancellationToken cancellationToken = default
        ) => throw Unavailable;

        public Task<UserAuth?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            throw Unavailable;

        public Task SaveAsync(UserAuth userAuth, CancellationToken cancellationToken) => throw Unavailable;

        public Task DeleteAsync(string email, CancellationToken cancellationToken) => throw Unavailable;
    }
}
