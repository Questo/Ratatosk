using Ratatosk.Domain.Identity;
using Ratatosk.Domain.Identity.Events;

namespace Ratatosk.UnitTests.Domain.Identity;

[TestClass]
public class UserTests
{
    private const string DUMMY_HASH = "foobar123";

    [TestMethod]
    public void Create_WithInvalidInput_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(string.Empty, UserRole.User, string.Empty)
        );
        Assert.Throws<ArgumentException>(() => User.Create(" ", UserRole.User, " "));
        Assert.Throws<ArgumentException>(() => User.Create(" ", UserRole.User, DUMMY_HASH));
        Assert.Throws<ArgumentException>(() => User.Create("invalid-email", UserRole.User, DUMMY_HASH));
        Assert.Throws<ArgumentException>(() => User.Create("test@example.com", UserRole.User, string.Empty));
    }

    [TestMethod]
    public void Create_WithValidInput_ShouldreturnUserWithCreatedEvent()
    {
        var email = Email.Create("test@example.com");
        var passwordHash = PasswordHash.Create(DUMMY_HASH);
        var user = User.Create(email.Value!.Value, UserRole.Admin, passwordHash.Value!.Value);

        Assert.IsNotNull(user);

        var @event = user.UncommittedEvents.OfType<UserCreated>().FirstOrDefault();

        Assert.IsNotNull(@event);
        Assert.AreEqual(email.Value, @event!.Email);
        Assert.AreEqual(UserRole.Admin, @event.Role);
        Assert.AreEqual(passwordHash.Value, @event.PasswordHash);
    }
}
