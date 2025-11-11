using Moq;
using Ratatosk.Application.Catalog.Queries;
using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Application.Shared;

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
        _handler = new(_readModelRepositoryMock.Object);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNoProductsExists_ShouldReturnSuccess()
    {
        var query = new SearchProductsQuery();

        _readModelRepositoryMock
            .Setup(x =>
                x.GetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Pagination<ProductReadModel>());

        var result = await _handler.HandleAsync(query);

        Assert.IsTrue(result.IsSuccess);
    }
}
