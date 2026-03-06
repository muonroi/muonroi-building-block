namespace Muonroi.BuildingBlock.Test;

public class DateTimeHelperTests
{
    [Fact]
    public void GetCurrentUnixTimestamp_Returns_Current_Timestamp()
    {
        double expected = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        double actual = DateTimeHelper.GetCurrentUnixTimestamp();
        Assert.InRange(actual, expected - 1, expected + 1);
    }

    [Fact]
    public void GetCurrentUnixTimestamp_NotAffected_By_Local_TimeZone()
    {
        double actual = DateTimeHelper.GetCurrentUnixTimestamp();
        double expected = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        Assert.InRange(actual, expected - 1, expected + 1);
    }

    [Fact]
    public void GetCurrentUnixTimestamp_Multiple_Calls_Return_Increasing_Values()
    {
        double prev = DateTimeHelper.GetCurrentUnixTimestamp();
        for (int i = 0; i < 5; i++)
        {
            SpinWait.SpinUntil(() =>
            {
                double current = DateTimeHelper.GetCurrentUnixTimestamp();
                return current > prev;
            }, 100);
            double current = DateTimeHelper.GetCurrentUnixTimestamp();
            Assert.True(current >= prev, $"Call {i} did not return an increasing value.");
            prev = current;
        }
    }
}
