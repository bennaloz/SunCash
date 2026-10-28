namespace SunCash.Domain.Calendar;

/// <summary>Fasce orarie ARERA (delibera 181/06) con cui le offerte a fasce prezzano l'energia.</summary>
public enum TariffBand
{
    /// <summary>Ore di punta: lunedì-venerdì 8-19, esclusi i festivi.</summary>
    F1 = 1,

    /// <summary>Ore intermedie: lunedì-venerdì 7-8 e 19-23, sabato 7-23, esclusi i festivi.</summary>
    F2 = 2,

    /// <summary>Ore fuori punta: lunedì-sabato 23-7, domeniche e festivi tutto il giorno.</summary>
    F3 = 3,
}
