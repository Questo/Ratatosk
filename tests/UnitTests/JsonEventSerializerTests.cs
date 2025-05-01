using System.Globalization;
using System.Text.RegularExpressions;
using Ratatosk.Infrastructure.EventStore;
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

        Assert.IsTrue(normalizedSerialized.Contains("Type"));
        Assert.IsTrue(normalizedSerialized.Contains("Ratatosk.UnitTests.JsonEventSerializerTests+TestEvent"));
    }

    [TestMethod]
    public void Deserialize_ShouldThrowIfTypeIsMissingOrInvalid()
    {
        var invalidJson = "{\"SomeProperty\": \"Test\"}"; // Missing 'Type' property

        Assert.ThrowsException<InvalidOperationException>(() => _serializer.Deserialize(invalidJson));
    }

    [TestMethod]
    public void Deserialize_ShouldReturnCorrectEventType()
    {
        var domainEvent = new TestEvent("acorn");
        var serialized = _serializer.Serialize(domainEvent);

        var deserializedEvent = _serializer.Deserialize(serialized);

        Assert.IsInstanceOfType<TestEvent>(deserializedEvent);
        Assert.AreEqual("acorn", ((TestEvent)deserializedEvent).Name);
    }
}

public static partial class RegexHelper
{
    [GeneratedRegex(@"\\u([0-9A-Fa-f]{4})")]
    private static partial Regex UnicodeEscapeRegex();

    public static string DecodeUnicodeEscapeSequences(string input)
    {
        var regex = UnicodeEscapeRegex();
        return regex.Replace(input, match =>
        {
            var code = match.Groups[1].Value;
            return char.ConvertFromUtf32(int.Parse(code, NumberStyles.HexNumber));
        });
    }
}