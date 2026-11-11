using DeskBox.Platform;

namespace DeskBox.Tests;

public sealed class LogitechBatteriesTests
{
    [Theory]
    [InlineData(4300, 100)]
    [InlineData(4186, 100)]
    [InlineData(3811, 50)]
    [InlineData(3500, 0)]
    [InlineData(3000, 0)]
    public void PercentFromMillivolts_MatchesTheCurveAnchors(int millivolts, int expected)
    {
        Assert.Equal(expected, LogitechBatteries.PercentFromMillivolts(millivolts));
    }

    [Fact]
    public void PercentFromMillivolts_InterpolatesBetweenAnchorsAndNeverDecreasesWithVoltage()
    {
        int previous = -1;
        for (int mv = 3400; mv <= 4300; mv += 5)
        {
            int percent = LogitechBatteries.PercentFromMillivolts(mv);
            Assert.InRange(percent, 0, 100);
            Assert.True(percent >= previous, $"{mv} mV gave {percent}% after {previous}%");
            previous = percent;
        }
    }

    [Theory]
    [InlineData(4044, 95)]
    [InlineData(4200, 100)]
    [InlineData(3830, 50)]
    [InlineData(3000, 0)]
    public void HeadsetCurve_MatchesCalibrationPoints(int millivolts, int expected)
    {
        Assert.Equal(expected, LogitechBatteries.PercentFromHeadsetMillivolts(millivolts));
    }
}
