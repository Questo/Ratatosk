using Moq;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.ReadModels;

namespace Ratatosk.UnitTests.Application.Catalog;

[TestClass]
public class GetProductsQueryHandlerTests
{
    private Mock<IProductReadModelRepository> _readModelRepositoryMock = null!;
    private SearchProductsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _readModelRepositoryMock = new Mock<IProductReadModelRepository>();
        _handler = new(
            _readModelRepositoryMock.Object
        );
    }

    // [TestMethod]
    // public async Task HandleAsync_WhenNoProductsExists_ShouldReturnSuccess()
    // {
    //     var query = new SearchProductsQuery();

    //     _readModelRepositoryMock
    //         .Setup(x => x.GetAllAsync(,It.IsAny<CancellationToken>()))
    //         .ReturnsAsync([]);

    //     var result = await _handler.HandleAsync(query);

    //     Assert.IsTrue(result.IsSuccess);
    // }
}
