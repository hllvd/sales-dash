using System;
using System.Collections.Generic;
using SalesApp.Libs;

public class Program {
    public static void Main() {
        var rawValue = "10134;901;X;TestUser;999901";
        var cotaInfo = CotaDecomposer.Decompose(rawValue);
        Console.WriteLine(cotaInfo.Contract);
    }
}
