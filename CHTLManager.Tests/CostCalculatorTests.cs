using CHTLManager.BLL;

namespace CHTLManager.Tests;

public class CostCalculatorTests
{
    [Theory]
    [InlineData(10, 100, 10, 120, 110)]
    [InlineData(0, 100, 5, 120, 120)]   // hết hàng: lấy giá nhập mới
    [InlineData(-1, 100, 5, 120, 120)]  // tồn âm không được kéo công thức sai
    [InlineData(3, 0, 3, 100, 50)]      // giá cũ 0 vẫn đúng công thức
    [InlineData(5, 100, 0, 999, 100)]   // không nhận gì: giữ nguyên
    [InlineData(0, 100, 0, 999, 100)]   // không chia cho 0
    public void AddToAverage_Cases(int stock, double average, int quantity, double cost, double expected)
        => Assert.Equal((decimal)expected, CostCalculator.AddToAverage(stock, (decimal)average, quantity, (decimal)cost));

    [Fact]
    public void AddToAverage_RoundsToTwoDecimals()
        => Assert.Equal(103.33m, CostCalculator.AddToAverage(2, 100, 1, 110));

    [Fact]
    public void RemoveFromAverage_RestoresOriginal()
        => Assert.Equal(100m, CostCalculator.RemoveFromAverage(20, 110, 10, 120));

    [Fact]
    public void RemoveFromAverage_AllStockRemoved_KeepsAverage()
        => Assert.Equal(110m, CostCalculator.RemoveFromAverage(10, 110, 10, 120));

    [Fact]
    public void RemoveFromAverage_MoreThanStock_KeepsAverage()
        => Assert.Equal(110m, CostCalculator.RemoveFromAverage(3, 110, 10, 120));

    [Fact]
    public void RemoveFromAverage_NeverNegative()
        => Assert.Equal(0m, CostCalculator.RemoveFromAverage(10, 10, 5, 1000));
}
