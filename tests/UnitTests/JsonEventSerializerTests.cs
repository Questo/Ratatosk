using System.Globalization;
using System.Text.RegularExpressions;
using Ratatosk.Domain;
using Ratatosk.Domain.Identity;
using Ratatosk.Domain.Identity.Events;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Infrastructure.Serialization.Serializers;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests;

[TestClass]
public class JsonEventSerializerTests
{
    private JsonEventSerializer _serializer = null!;

    [TestInitialize]
    public void Setup()
    {
        _serializer = new JsonEventSerializer();
    }

    [TestMethod]
    public void Serialize_ShouldIncludeTypeInSerializedData()
    {
        var domainEvent = new TestEvent("acorn");

        var serialized = _serializer.Serialize(domainEvent);
        var normalizedSerialized = RegexHelper.DecodeUnicodeEscapeSequences(serialized);

        Assert.Contains("Type", normalizedSerialized);
        Assert.Contains("Ratatosk.UnitTests.Shared.TestEvent", normalizedSerialized);
    }

    [TestMethod]
    public void Deserialize_ShouldThrowIfTypeIsMissingOrInvalid()
    {
        var invalidJson = "{\"SomeProperty\": \"Test\"}"; // Missing 'Type' property

        Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(invalidJson));
    }

    [TestMethod]
    public void Deserialize_ShouldReturnCorrectEventType()
    {
        var domainEvent = new TestEvent("acorn");
        var serialized = _serializer.Serialize(domainEvent);

        var deserializedEvent = _serializer.Deserialize(serialized);

        Assert.IsInstanceOfType<TestEvent>(deserializedEvent);
        Assert.AreEqual("acorn", ((TestEvent)deserializedEvent).Value);
    }

    [TestMethod]
    public void Serialize_StoresTheTypeWithoutAnAssemblyVersion()
    {
        var serialized = _serializer.Serialize(new TestEvent("acorn"));

        StringAssert.Contains(serialized, "\"Type\":\"Ratatosk.UnitTests.Shared.TestEvent\"");
        Assert.DoesNotContain("Version=", serialized);
    }

    [TestMethod]
    public void Deserialize_LegacyAssemblyQualifiedTypeName_StillResolves()
    {
        var legacy = _serializer
            .Serialize(new TestEvent("acorn"))
            .Replace(
                "\"Ratatosk.UnitTests.Shared.TestEvent\"",
                $"\"{typeof(TestEvent).AssemblyQualifiedName}\""
            );

        var deserialized = _serializer.Deserialize(legacy);

        Assert.AreEqual("acorn", ((TestEvent)deserialized).Value);
    }

    [TestMethod]
    public void RoundTrip_PreservesWhenTheEventOccurred()
    {
        var domainEvent = new TestEvent("acorn") { OccurredAtUtc = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero) };

        var deserialized = _serializer.Deserialize(_serializer.Serialize(domainEvent));

        Assert.AreEqual(domainEvent.OccurredAtUtc, deserialized.OccurredAtUtc);
    }

    [TestMethod]
    public void Deserialize_StoredValueThatTodaysValidationRejects_StillLoads()
    {
        var stored = _serializer
            .Serialize(new StockAdded(Guid.NewGuid(), SKU.Create("TS-ABCD1234").Value!, Quantity.Pieces(3)))
            .Replace("TS-ABCD1234", "legacy_sku");

        var deserialized = (StockAdded)_serializer.Deserialize(stored);

        Assert.AreEqual("legacy_sku", deserialized.SKU.Value);
    }

    [TestMethod]
    public void RoundTrip_UserCreated_RestoresIdentityValueObjects()
    {
        var created = new UserCreated(
            Guid.NewGuid(),
            Email.Create("someone@example.com").Value!,
            UserRole.Admin,
            PasswordHash.Create("hash").Value!
        );

        var deserialized = (UserCreated)_serializer.Deserialize(_serializer.Serialize(created));

        Assert.AreEqual(created.UserId, deserialized.UserId);
        Assert.AreEqual(created.Email, deserialized.Email);
        Assert.AreEqual(created.PasswordHash, deserialized.PasswordHash);
        Assert.AreEqual(UserRole.Admin, deserialized.Role);
    }

    [TestMethod]
    public void Deserialize_UserCreatedStoredWithoutAUserId_LoadsWithAnEmptyId()
    {
        var userId = Guid.NewGuid();
        var stored = _serializer
            .Serialize(
                new UserCreated(
                    userId,
                    Email.Create("someone@example.com").Value!,
                    UserRole.User,
                    PasswordHash.Create("hash").Value!
                )
            )
            .Replace($"\"UserId\":\"{userId}\",", "");
        Assert.DoesNotContain("UserId", stored);

        var deserialized = (UserCreated)_serializer.Deserialize(stored);

        Assert.AreEqual(Guid.Empty, deserialized.UserId);
        Assert.AreEqual("someone@example.com", deserialized.Email.Value);
    }
}

public static partial class RegexHelper
{
    [GeneratedRegex(@"\\u([0-9A-Fa-f]{4})")]
    private static partial Regex UnicodeEscapeRegex();

    public static string DecodeUnicodeEscapeSequences(string input)
    {
        var regex = UnicodeEscapeRegex();
        return regex.Replace(
            input,
            match =>
            {
                var code = match.Groups[1].Value;
                return char.ConvertFromUtf32(int.Parse(code, NumberStyles.HexNumber));
            }
        );
    }
}
