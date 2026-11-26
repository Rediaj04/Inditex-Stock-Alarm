using InditexStockAlarm;

namespace InditexStockAlarm.Tests;

public class ItemUrlTests
{
    [Theory]
    [InlineData("https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428", "428")]
    [InlineData("https://www.pullandbear.com/es/sudadera-cuello-cremallera-l07558508?pelement=748920134&cS=485", "485")]
    [InlineData("https://www.zara.com/es/es/camiseta-tank-rib-basica-p04087450.html", "")]
    public void ParseaColorDeLaUrl(string url, string expectedColor)
    {
        Assert.True(ItemUrl.TryParse(url, out var colorId, out var error), error);
        Assert.Equal(expectedColor, colorId);
    }

    [Theory]
    [InlineData("https://example.com/x.html")]
    [InlineData("")]
    [InlineData("no es una url")]
    public void RechazaUrlsNoInditex(string url)
    {
        Assert.False(ItemUrl.TryParse(url, out _, out _));
    }

    [Fact]
    public void ParseaTallas()
    {
        Assert.Equal(new[] { "42", "44", "46" }, ItemUrl.ParseSizes("42, 44;46"));
        Assert.Equal(new[] { "M", "L" }, ItemUrl.ParseSizes("M,L,M"));
        Assert.Empty(ItemUrl.ParseSizes("   "));
    }

    [Theory]
    [InlineData("https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428", "Jeans barrel")]
    [InlineData("https://www.pullandbear.com/es/sudadera-cuello-cremallera-l07558508?cS=485", "Sudadera cuello cremallera")]
    [InlineData("https://www.zara.com/es/es/camiseta-tank-rib-basica-p04087450.html", "Camiseta tank rib basica")]
    public void DeduceNombre(string url, string expected)
    {
        Assert.Equal(expected, ItemUrl.GuessName(url));
    }
}

public class SizeResultTests
{
    [Fact]
    public void CalculaDisponibilidadYTexto()
    {
        Assert.True(new SizeResult { Visibility = "SHOW" }.Available);
        Assert.True(new SizeResult { Visibility = "RUNNING_OUT" }.Available);
        Assert.False(new SizeResult { Visibility = "SOLD_OUT" }.Available);
        Assert.False(new SizeResult { Visibility = "NO_EXISTE" }.Available);

        Assert.Equal("¡POCAS!", new SizeResult { Visibility = "RUNNING_OUT" }.Human);
        Assert.Equal("no existe", new SizeResult { Visibility = "NO_EXISTE" }.Human);
        Assert.Equal("agotada", new SizeResult { Visibility = "SOLD_OUT" }.Human);
    }
}

public class AppConfigTests
{
    [Fact]
    public void GuardaYCargaLaConfiguracion()
    {
        var dir = Path.Combine(Path.GetTempPath(), "inditex-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var config = new AppConfig
            {
                IntervalSeconds = 30,
                SoundEnabled = false,
                AllowFastInterval = true,
                NtfyTopic = "tema-prueba"
            };
            config.Items.Add(new ItemConfig
            {
                Name = "X",
                Url = "https://www.bershka.com/es/x-c0p1.html?colorId=2",
                Sizes = "44,46"
            });

            config.Save(dir);
            var loaded = AppConfig.Load(dir);

            Assert.Equal(30, loaded.IntervalSeconds);
            Assert.False(loaded.SoundEnabled);
            Assert.True(loaded.AllowFastInterval);
            Assert.Equal("tema-prueba", loaded.NtfyTopic);
            Assert.Single(loaded.Items);
            Assert.Equal("44,46", loaded.Items[0].Sizes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
