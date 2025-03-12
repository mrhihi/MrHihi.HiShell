using System.Text.RegularExpressions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace MrHihi.HiShell;
public class NuGetInstaller
{
    private readonly string _baseDirectory;
    public string NugetPackagesDirName { get; set;} = "nuget_packages";
    public string NugetPackageDir => Path.Combine(_baseDirectory, NugetPackagesDirName);
    public NuGetInstaller(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }
    // 解析並下載 NuGet 套件
    public async Task<(string script, IEnumerable<string> references)> ProcessNugetReferences(string scriptContent)
    {
        var nugetRegex = new Regex(@"#r\s+""nuget:\s*([^,]+),\s*([^""]+)""", RegexOptions.Multiline);
        var matches = nugetRegex.Matches(scriptContent);
        var references = new List<string>();

        if (matches.Count == 0)
        {
            return (scriptContent, references);
        }

        // 配置 NuGet 來源
        var logger = NullLogger.Instance;
        var cache = new SourceCacheContext();
        var source = new PackageSource("https://api.nuget.org/v3/index.json");
        var repository = Repository.Factory.GetCoreV3(source);

        foreach (Match match in matches)
        {
            string packageId = match.Groups[1].Value.Trim();
            string version = match.Groups[2].Value.Trim();

            // 下載 NuGet 套件
            var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            var packageVersion = new NuGetVersion(version);
            var packagePath = await downloadPackage(resource, packageId, packageVersion, logger, cache);

            // 提取 DLL 檔案並加入參考
            var dllFiles = ExtractDlls(packagePath);
            references.AddRange(dllFiles);
        }

        // 移除 #r "nuget: ..." 指令
        string processedScript = nugetRegex.Replace(scriptContent, "");
        return (processedScript, references);
    }

    // 下載 NuGet 套件到本地
    private async Task<string> downloadPackage(FindPackageByIdResource resource, string packageId, NuGetVersion version, ILogger logger, SourceCacheContext cache)
    {
        string packagesDir = NugetPackageDir;
        Directory.CreateDirectory(packagesDir);

        string packageFile = Path.Combine(packagesDir, $"{packageId}.{version}.nupkg");
        if (!File.Exists(packageFile))
        {
            using var packageStream = File.Create(packageFile);
            // 提供 CancellationToken
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 設定 30 秒超時
            await resource.CopyNupkgToStreamAsync(packageId, version, packageStream, cache, logger, cts.Token);
        }
        return packageFile;
    }

    // 提取 DLL 檔案
    public static IEnumerable<string> ExtractDlls(string packagePath)
    {
        var directoryName = Path.GetDirectoryName(packagePath) ?? throw new ArgumentNullException(nameof(packagePath), "Package path directory name is null");
        var extractDir = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(packagePath));
        if (!Directory.Exists(extractDir))
        {
            Directory.CreateDirectory(extractDir);
            using var packageReader = new PackageArchiveReader(packagePath);
            var libFiles = packageReader.GetLibItems()
                .SelectMany(g => g.Items)
                .Where(i => i.EndsWith(".dll"));

            foreach (var file in libFiles)
            {
                var destPath = Path.Combine(extractDir, Path.GetFileName(file));
                packageReader.ExtractFile(file, destPath, NullLogger.Instance);
                yield return destPath;
            }
        }
        else
        {
            foreach (var dll in Directory.GetFiles(extractDir, "*.dll", SearchOption.AllDirectories))
            {
                yield return dll;
            }
        }
    }

    public void CleanNugetPackages()
    {
        if (Directory.Exists(NugetPackageDir))
        {
            foreach (var nupkg in Directory.GetFiles(NugetPackageDir, "*.nupkg", SearchOption.TopDirectoryOnly))
            {
                File.Delete(nupkg);
            }
            foreach (var dll in Directory.GetFiles(NugetPackageDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                File.Delete(dll);
            }
            // 列出其下所有目錄
            foreach (var dir in Directory.GetDirectories(NugetPackageDir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    // 解析 using 語句
    public static IEnumerable<string> GetImports(string scriptContent)
    {
        var importRegex = new Regex(@"using\s+([^;]+);", RegexOptions.Multiline);
        return importRegex.Matches(scriptContent).Select(m => m.Groups[1].Value.Trim());
    }
}