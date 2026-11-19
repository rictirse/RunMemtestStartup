using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

// RunMemtest launcher:
// extracts the embedded package (Package/**) to %TEMP%\RunMemtest\<version>-<content hash>
// and starts RunMemtest.exe with the launcher's working directory as argument
// (RunMemtest reads/writes appsettings.json and screenshots there).

const string AppName = "RunMemtest";
const string ResourcePrefix = "Package/";
const string CompleteMarker = ".extracted";

var assembly = Assembly.GetExecutingAssembly();
var resources = assembly.GetManifestResourceNames()
    .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();

if (resources.Length == 0)
{
    Console.WriteLine("No package is embedded in this build.");
    return 1;
}

var exportPath = BuildExportPath(assembly, resources);

// An interrupted extraction leaves no marker, so it is extracted again.
if (!File.Exists(Path.Combine(exportPath, CompleteMarker)))
{
    Export(assembly, resources, exportPath);
}

StartMemtest(exportPath);
return 0;

static string BuildExportPath(Assembly assembly, string[] resources)
{
    var version = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion ?? "0.0.0.0";
    // Content hash: a rebuilt package with an unchanged version never reuses old files.
    var folder = $"{version}-{ComputeContentHash(assembly, resources)}";
    var exportPath = Path.Combine(Path.GetTempPath(), AppName, folder);
    Directory.CreateDirectory(exportPath);
    return exportPath;
}

static string ComputeContentHash(Assembly assembly, string[] resources)
{
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var buffer = new byte[81920];
    foreach (var name in resources)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(name));
        using var stream = assembly.GetManifestResourceStream(name)!;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }
    }

    return Convert.ToHexString(hash.GetHashAndReset())[..12].ToLowerInvariant();
}

static void Export(Assembly assembly, string[] resources, string exportPath)
{
    var root = Path.GetFullPath(exportPath) + Path.DirectorySeparatorChar;
    foreach (var name in resources)
    {
        // Resource names keep the package's relative path (see LogicalName in the csproj).
        var relative = name[ResourcePrefix.Length..].Replace('\\', '/');
        var target = Path.GetFullPath(Path.Combine(exportPath, relative));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var source = assembly.GetManifestResourceStream(name)!;
        using var file = new FileStream(target, FileMode.Create, FileAccess.Write);
        source.CopyTo(file);
        Console.WriteLine($"Export {relative} done.");
    }

    File.WriteAllText(Path.Combine(exportPath, CompleteMarker), DateTime.UtcNow.ToString("O"));
}

static void StartMemtest(string exportPath)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = Path.Combine(exportPath, $"{AppName}.exe"),
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    // ArgumentList quotes the path, so folders with spaces stay one argument.
    startInfo.ArgumentList.Add(Environment.CurrentDirectory);
    Process.Start(startInfo);
}
