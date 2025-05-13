using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class DispatcherTests
{
    public record DummyRequest(string Value) : IRequest<string>;

    public class DummyRequestHandler : IRequestHandler<DummyRequest, string>
    {
        public Task<string> HandleAsync(DummyRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Handled: {request.Value}");
        }
    }

    [TestMethod]
    public async Task DispatchAsync_Should_Invoke_Correct_Handler_And_Return_Result()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<DummyRequest, string>, DummyRequestHandler>();
        var provider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(provider);
        var request = new DummyRequest("Test");

        // Act
        var result = await dispatcher.DispatchAsync<string>(request);

        // Assert
        Assert.AreEqual("Handled: Test", result);
    }

    [TestMethod]
    public async Task DispatchAsync_Should_Pass_CancellationToken()
    {
        // Arrange
        var tokenSource = new CancellationTokenSource();
        var tokenUsed = false;

        var handler = new TestHandler(ct => tokenUsed = ct == tokenSource.Token);
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<DummyRequest, string>>(handler);
        var provider = services.BuildServiceProvider();

        var dispatcher = new Dispatcher(provider);
        var request = new DummyRequest("Cancel");

        // Act
        await dispatcher.DispatchAsync<string>(request, tokenSource.Token);

        // Assert
        Assert.IsTrue(tokenUsed);
    }

    private class TestHandler(Action<CancellationToken> onInvoke) : IRequestHandler<DummyRequest, string>
    {
        public Task<string> HandleAsync(DummyRequest request, CancellationToken cancellationToken)
        {
            onInvoke(cancellationToken);
            return Task.FromResult("OK");
        }
    }

    [TestMethod]
    public void DispatchAsync_Should_Throw_If_Handler_Not_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(provider);
        var request = new DummyRequest("Missing");

        // Act
        Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await dispatcher.DispatchAsync<string>(request));
    }
}
