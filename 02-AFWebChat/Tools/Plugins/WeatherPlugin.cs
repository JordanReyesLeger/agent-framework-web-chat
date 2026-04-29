using System.ComponentModel;

namespace AFWebChat.Tools.Plugins;

public class WeatherPlugin
{
    [Description("Get the current weather for a given city")]
    public static string GetCurrentWeather(
        [Description("The city name to get weather for")] string city)
    {
        // Simulated weather data for demo purposes
        var random = new Random(city.GetHashCode());
        var temp = random.Next(-5, 40);
        var conditions = new[] { "sunny", "cloudy", "rainy", "partly cloudy", "overcast", "stormy" };
        var condition = conditions[random.Next(conditions.Length)];
        var humidity = random.Next(30, 95);

        return $"Weather in {city}: {temp}°C, {condition}, humidity {humidity}%";
    }

    [Description("Get the weather forecast for a given city for a number of days")]
    public static string GetForecast(
        [Description("The city name to get forecast for")] string city,
        [Description("Number of days for the forecast (1-7)")] int days = 3)
    {
        days = Math.Clamp(days, 1, 7);
        var random = new Random(city.GetHashCode());
        var lines = new List<string> { $"Forecast for {city} ({days} days):" };

        for (int i = 1; i <= days; i++)
        {
            var temp = random.Next(-5, 40);
            var conditions = new[] { "sunny", "cloudy", "rainy", "partly cloudy" };
            var condition = conditions[random.Next(conditions.Length)];
            lines.Add($"  Day {i}: {temp}°C, {condition}");
        }

        return string.Join("\n", lines);
    }
}
