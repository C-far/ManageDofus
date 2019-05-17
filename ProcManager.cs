using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

public static class ProcManager
{
    public static List<PRC> GetAllProcesses() {
        var pStartInfo = new ProcessStartInfo();
        pStartInfo.FileName = "netstat.exe";
        pStartInfo.Arguments = "-a -n -o";
        pStartInfo.WindowStyle = ProcessWindowStyle.Maximized;
        pStartInfo.UseShellExecute = false;
        pStartInfo.RedirectStandardInput = true;
        pStartInfo.RedirectStandardOutput = true;
        pStartInfo.RedirectStandardError = true;

        var process = new Process() {
            StartInfo = pStartInfo
        };
        process.Start();

        var soStream = process.StandardOutput;

        var output = soStream.ReadToEnd();
        if (process.ExitCode != 0)
            throw new Exception("something broke");

        var result = new List<PRC>();

        var lines = Regex.Split(output, "\r\n");
        foreach (var line in lines) {
            if (line.Trim().StartsWith("Proto"))
                continue;

            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var len = parts.Length;
            if (len > 2) {
                result.Add(new PRC {
                    Protocol = parts[0],
                    Port = int.Parse(parts[1].Split(':').Last()),
                    PID = int.Parse(parts[len - 1])
                });
            }
        }
        return result;
    }
}