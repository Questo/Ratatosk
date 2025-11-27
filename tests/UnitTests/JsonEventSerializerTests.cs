using System.Globalization;
using System.Text.RegularExpressions;
using Ratatosk.Infrastructure.Serialization;
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
