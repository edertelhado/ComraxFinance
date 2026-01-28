// @created : 28/01/2026
// Copyright (c) 2026 Eder Rafael Telhado. Uso sujeito aos termos da licença LSPR-REVOGÁVEL.

namespace ComraxFinance;

public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}