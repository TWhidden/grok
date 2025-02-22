namespace GrokSdk.Tests.Helpers;

[TestClass]
public class SatelliteHelperTests
{
    [TestMethod]
    public async Task TestStarlinkCount()
    {
        var satellites = await SatelliteHelper.GetSatellitesAsync(52);
        Assert.IsTrue(satellites.Count > 100, "Satellite Count should not be < 100");
    }
}