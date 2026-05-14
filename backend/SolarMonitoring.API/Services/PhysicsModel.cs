using SolarMonitoring.API.Models;

namespace SolarMonitoring.API.Services;

/// Deterministic clear-sky-derated forecast for one hour.
/// Inputs: installation geometry + Open-Meteo's GHI + ambient temp.
/// Output: predicted PV power (W) for that hour.
///
/// Treats the array as south-facing; non-south azimuth will show up as
/// systematic residual that the ML correction layer learns.
public class PhysicsModel
{
    private const double TempCoeff = 0.004;   // 0.4 %/°C derating (typical c-Si)
    private const double NoctRise = 25.0;     // cell-temp rise at POA=800 W/m²

    public double PredictPowerWatts(Installation installation, DateTime utcHour, HourlyWeather weather)
    {
        var (cosZenith, cosIncidence) = SunGeometry(installation, utcHour);
        if (cosZenith <= 0 || cosIncidence <= 0)
            return 0;

        // Project horizontal irradiance onto tilted plane (oversimplified — fine baseline).
        var poaFactor = Math.Min(1.5, cosIncidence / Math.Max(0.05, cosZenith));
        var poa = Math.Max(0, weather.ShortwaveRadiation) * poaFactor;

        // Cell temperature approximation (Sandia NOCT-style).
        var cellTempC = weather.TemperatureC + NoctRise * poa / 800.0;
        var tempDerate = 1.0 - TempCoeff * (cellTempC - 25.0);

        var dcPower = poa * installation.SystemCapacityWatts / 1000.0 * tempDerate;
        // Clip at inverter rating; clamp negative derating near zero output.
        return Math.Clamp(dcPower, 0, installation.SystemCapacityWatts);
    }

    private static (double cosZenith, double cosIncidence) SunGeometry(Installation installation, DateTime utcHour)
    {
        var dayOfYear = utcHour.DayOfYear;
        var declinationDeg = 23.45 * Math.Sin(DegToRad(360.0 / 365.0 * (284 + dayOfYear)));
        var declination = DegToRad(declinationDeg);

        // Approximate solar time = UTC + longitude/15 h (ignore equation-of-time; ≤15 min error).
        var solarHour = utcHour.Hour + utcHour.Minute / 60.0 + installation.Longitude / 15.0;
        var hourAngle = DegToRad(15.0 * (solarHour - 12.0));

        var lat = DegToRad(installation.Latitude);
        var tilt = DegToRad(installation.PanelTiltDeg);

        var cosZenith = Math.Sin(lat) * Math.Sin(declination)
                      + Math.Cos(lat) * Math.Cos(declination) * Math.Cos(hourAngle);

        // Incidence angle on a south-facing tilted plane.
        var cosIncidence = Math.Sin(declination) * Math.Sin(lat - tilt)
                         + Math.Cos(declination) * Math.Cos(lat - tilt) * Math.Cos(hourAngle);

        return (cosZenith, cosIncidence);
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;
}
