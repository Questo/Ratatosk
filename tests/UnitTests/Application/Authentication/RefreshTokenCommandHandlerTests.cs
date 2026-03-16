using Moq;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Primitives;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class RefreshTokenCommandHandlerTests
{
    private Mock<IUserAuthRepository> _userAuthRepositoryMock = null!;
    private Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = null!;
    private Mock<ITokenIssuer> _tokenIssuerMock = null!;
    private RefreshTokenCommandHandler _handler = null!;

    private const string ValidEmail = "user@example.com";
    private const string TokenValue = "existing_refresh_token";

    [TestInitialize]
    public void Setup()
    {
        _userAuthRepositoryMock = new Mock<IUserAuthRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _tokenIssuerMock = new Mock<ITokenIssuer>();

        _handler = new RefreshTokenCommandHandler(
            _userAuthRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _tokenIssuerMock.Object
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_New_TokenPair_And_Rotate_Token_When_Valid()
    {
        // Arrange
        var storedToken = new RefreshToken(TokenValue, ValidEmail, DateTimeOffset.UtcNow.AddDays(7));
        var userAuth = new UserAuth(ValidEmail, "User", "hash");

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenAsync(TokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAuth);

        _tokenIssuerMock
            .Setup(x => x.IssueToken(ValidEmail, "User"))
            .Returns(Result<string>.Success("new_access_token"));

        // Act
        var result = await _handler.HandleAsync(new RefreshTokenCommand(TokenValue));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("new_access_token", result.Value!.AccessToken);
        Assert.IsFalse(string.IsNullOrEmpty(result.Value.RefreshToken));
        Assert.AreNotEqual(TokenValue, result.Value.RefreshToken);

        // Old token revoked
        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeAsync(TokenValue, It.IsAny<CancellationToken>()),
            Times.Once
        );

        // New token saved
        _refreshTokenRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Not_Found()
    {
        // Arrange
        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenAsync(TokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _handler.HandleAsync(new RefreshTokenCommand(TokenValue));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidRefreshToken.Message, result.Error);

        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Is_Expired()
    {
        // Arrange
        var expiredToken = new RefreshToken(TokenValue, ValidEmail, DateTimeOffset.UtcNow.AddDays(-1));

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenAsync(TokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        // Act
        var result = await _handler.HandleAsync(new RefreshTokenCommand(TokenValue));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidRefreshToken.Message, result.Error);

        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_User_No_Longer_Exists()
    {
        // Arrange
        var storedToken = new RefreshToken(TokenValue, ValidEmail, DateTimeOffset.UtcNow.AddDays(7));

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenAsync(TokenValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuth?)null);

        // Act
        var result = await _handler.HandleAsync(new RefreshTokenCommand(TokenValue));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidRefreshToken.Message, result.Error);
    }
}
