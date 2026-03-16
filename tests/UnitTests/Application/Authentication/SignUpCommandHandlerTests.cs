using Moq;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Commands;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class SignUpCommandHandlerTests
{
    private Mock<IAggregateRepository<User>> _userRepositoryMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IUserAuthRepository> _userAuthRepositoryMock = null!;
    private Mock<IPasswordHasher> _passwordHasherMock = null!;
    private Mock<ITokenIssuer> _tokenIssuerMock = null!;
    private SignUpCommandHandler _handler = null!;

    private const string ValidEmail = "user@example.com";
    private const string ValidPassword = "password";

    [TestInitialize]
    public void Setup()
    {
        _userRepositoryMock = new Mock<IAggregateRepository<User>>();
        _eventBusMock = new Mock<IEventBus>();
        _userAuthRepositoryMock = new Mock<IUserAuthRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenIssuerMock = new Mock<ITokenIssuer>();

        _handler = new SignUpCommandHandler(
            _userRepositoryMock.Object,
            _eventBusMock.Object,
            _userAuthRepositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenIssuerMock.Object
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_AccessToken_When_SignUp_Succeeds()
    {
        // Arrange
        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuth?)null);

        _passwordHasherMock
            .Setup(x => x.Hash(It.IsAny<Password>()))
            .Returns(PasswordHash.Create("hashed").Value!);

        _tokenIssuerMock
            .Setup(x => x.IssueToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<string>.Success("access_token"));

        // Act
        var result = await _handler.HandleAsync(new SignUpCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("access_token", result.Value);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Save_User_And_Publish_Events_When_SignUp_Succeeds()
    {
        // Arrange
        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuth?)null);

        _passwordHasherMock
            .Setup(x => x.Hash(It.IsAny<Password>()))
            .Returns(PasswordHash.Create("hashed").Value!);

        _tokenIssuerMock
            .Setup(x => x.IssueToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<string>.Success("access_token"));

        // Act
        await _handler.HandleAsync(new SignUpCommand(ValidEmail, ValidPassword));

        // Assert
        _userRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Account_Already_Exists()
    {
        // Arrange
        var existingUser = new UserAuth(ValidEmail, "User", "hash");

        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.HandleAsync(new SignUpCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.AccountAlreadyExists.Message, result.Error);

        _userRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Password_Is_Empty()
    {
        // Arrange
        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuth?)null);

        // Act — empty password causes Password.Create to fail
        var result = await _handler.HandleAsync(new SignUpCommand(ValidEmail, ""));

        // Assert
        Assert.IsTrue(result.IsFailure);

        _userRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Issuer_Fails()
    {
        // Arrange
        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuth?)null);

        _passwordHasherMock
            .Setup(x => x.Hash(It.IsAny<Password>()))
            .Returns(PasswordHash.Create("hashed").Value!);

        _tokenIssuerMock
            .Setup(x => x.IssueToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<string>.Failure("signing error"));

        // Act
        var result = await _handler.HandleAsync(new SignUpCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(Errors.Authentication.InvalidToken.Message, result.Error);
    }

    [TestMethod]
    public async Task HandleAsync_Should_Return_Failure_When_Exception_Is_Thrown()
    {
        // Arrange
        _userAuthRepositoryMock
            .Setup(x => x.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        // Act
        var result = await _handler.HandleAsync(new SignUpCommand(ValidEmail, ValidPassword));

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("database unavailable", result.Error);
    }
}
