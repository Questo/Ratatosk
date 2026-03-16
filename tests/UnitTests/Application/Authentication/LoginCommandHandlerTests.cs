using Moq;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class LoginCommandHandlerTests
{
    private Mock<IUserAuthRepository> _userAuthRepositoryMock = null!;
    private Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = null!;
    private Mock<IPasswordHasher> _passwordHasherMock = null!;
    private Mock<ITokenIssuer> _tokenIssuerMock = null!;
    private LoginCommandHandler _handler = null!;

    private const string ValidEmail = "user@example.com";
    private const string ValidPassword = "password";
    private const string StoredHash = "argon2hash";

    [TestInitialize]
    public void Setup()
    {
        _userAuthRepositoryMock = new Mock<IUserAuthRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenIssuerMock = new Mock<ITokenIssuer>();

        _handler = new LoginCommandHandler(
            _userAuthRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenIssuerMock.Object
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_TokenPair_When_Credentials_Are_Valid()
    {
        // Arrange
        var userAuth = new UserAuth(ValidEmail, "User", StoredHash);

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAuth);

        _passwordHasherMock
            .Setup(x => x.Verify(It.IsAny<Password>(), It.IsAny<PasswordHash>()))
            .Returns(true);

        _tokenIssuerMock
            .Setup(x => x.IssueToken(ValidEmail, "User"))
            .Returns(Result<string>.Success("access_token"));

        // Act
        var result = await _handler.HandleAsync(new LoginCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("access_token", result.Value!.AccessToken);
        Assert.IsFalse(string.IsNullOrEmpty(result.Value.RefreshToken));

        _refreshTokenRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_User_Not_Found()
    {
        // Arrange
        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuth?)null);

        // Act
        var result = await _handler.HandleAsync(new LoginCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidCredentials.Message, result.Error);

        _refreshTokenRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Password_Does_Not_Match()
    {
        // Arrange
        var userAuth = new UserAuth(ValidEmail, "User", StoredHash);

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAuth);

        _passwordHasherMock
            .Setup(x => x.Verify(It.IsAny<Password>(), It.IsAny<PasswordHash>()))
            .Returns(false);

        // Act
        var result = await _handler.HandleAsync(new LoginCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidCredentials.Message, result.Error);

        _refreshTokenRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Password_Is_Empty()
    {
        // Arrange
        var userAuth = new UserAuth(ValidEmail, "User", StoredHash);

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAuth);

        // Act — empty password causes Password.Create to fail
        var result = await _handler.HandleAsync(new LoginCommand(ValidEmail, ""));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidCredentials.Message, result.Error);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Issuer_Fails()
    {
        // Arrange
        var userAuth = new UserAuth(ValidEmail, "User", StoredHash);

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAuth);

        _passwordHasherMock
            .Setup(x => x.Verify(It.IsAny<Password>(), It.IsAny<PasswordHash>()))
            .Returns(true);

        _tokenIssuerMock
            .Setup(x => x.IssueToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<string>.Failure("signing error"));

        // Act
        var result = await _handler.HandleAsync(new LoginCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidToken.Message, result.Error);
    }
}
