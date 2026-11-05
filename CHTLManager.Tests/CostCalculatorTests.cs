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
    public void ReplaceInAverage_ChangesOnlyTheReplacedUnits()
        => Assert.Equal(13500m, CostCalculator.ReplaceInAverage(8, 12000, 3, 12000, 16000));

    [Fact]
    public void ReplaceInAverage_NoStock_KeepsAverage()
        => Assert.Equal(100m, CostCalculator.ReplaceInAverage(0, 100, 2, 100, 110));

    [Fact]
    public void ReplaceInAverage_NoOldAverage_UsesNewCostForReplacedUnits()
        => Assert.Equal(110m, CostCalculator.ReplaceInAverage(2, 0, 2, 0, 110));

    [Fact]
    public void ReplaceInAverage_NeverNegative()
        => Assert.Equal(0m, CostCalculator.ReplaceInAverage(2, 10, 2, 1000, 0));

    [Fact]
    public void RemoveFromAverage_NeverNegative()
        => Assert.Equal(0m, CostCalculator.RemoveFromAverage(10, 10, 5, 1000));
}
