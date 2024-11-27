﻿using System.Diagnostics;
using System.Reflection;
const string RunMemtest = nameof(RunMemtest);

var exportPath = BuildExportPath();
Export(exportPath);
CopyRuntime(exportPath);
StartMemtest(exportPath);

static string BuildExportPath()
{
    var assemblyVersion = $"{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}";
    var exportPath = Path.Combine(Path.GetTempPath(), RunMemtest, assemblyVersion);
    exportPath.SafeCreate();

    return exportPath;
}

static void CopyRuntime(string exportPath)
{
    var runtimePath = Path.Combine(exportPath, "runtimes", "win", "lib", "net8.0");
    runtimePath.SafeCreate();

    const string systemManagement = "System.Management.dll";
    var systemManagementPath = Path.Combine(runtimePath, systemManagement);
    var fi_systemManagement = new FileInfo(systemManagementPath);
    if (!fi_systemManagement.Exists)
    {
        systemManagementPath = Path.Combine(exportPath, systemManagement);
        var fi = new FileInfo(systemManagementPath);
        fi.CopyTo(fi_systemManagement.FullName);
    }
}

static void Export(string exportPath)
{
    var assembly = Assembly.GetEntryAssembly()?.GetManifestResourceNames() ?? Enumerable.Empty<string>();

    foreach (var assemblyPath in assembly)
    {
        EmbeddedResourceToFile(exportPath, assemblyPath);
    }
}

static void EmbeddedResourceToFile(string exportPath, string assemblyPath)
{
    byte[]? ba = null;
    var assemblyName = assemblyPath[26..^0];
    var filePath = new FileInfo(Path.Combine(exportPath, assemblyName));
    if (filePath.Exists)
    {
        Console.WriteLine($"{assemblyName} exist.");
#if DEBUG
        filePath.Delete();
#else
            return;
#endif
    }

    try
    {
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream(assemblyPath)!)
        {
            ba = new byte[(int)stream.Length];
            stream.Read(ba, 0, (int)stream.Length);
        }

        using (var fs = new FileStream(filePath.FullName, FileMode.Create))
        {
            using (var bw = new BinaryWriter(fs))
                bw.Write(ba);
        }
        Console.WriteLine($"Export {assemblyName} done.");
    }
    catch (Exception)
    {
        Console.WriteLine($"Export {assemblyName} failed.");
    }
}

static void StartMemtest(string exportPath)
{
    RunCmd(Path.Combine(exportPath, "RunMemtest.exe"));
}

static void RunCmd(string fileName)
{
    var p = CreatProcess(fileName);
    p.Start();
}

static Process CreatProcess(string fileName)
{ 
    return new Process()
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        }
    };
}

internal static class ExtensionFunc
{
    internal static void SafeCreate(this string dirPath)
    {
        var di = new DirectoryInfo(dirPath);
        di.SafeCreate();
    }

    static void SafeCreate(this DirectoryInfo di)
    {
        if (di.Exists) return;
        di.Create();
    }
}