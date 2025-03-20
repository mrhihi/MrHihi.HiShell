using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace MrHihi.HiShell;

public class NuGetInstaller
{
    private readonly string _workingDirectory;
    public string NugetPackagesDirName { get; set; } = "nuget_packages";
    public string NugetPackageDir => Path.Combine(_workingDirectory, NugetPackagesDirName);
    private readonly ISettings _nugetSettings;
    private readonly NuGetFramework _currentFramework;
    private static readonly bool _isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public NuGetInstaller(string baseDirectory)
    {
        _workingDirectory = baseDirectory;
        _nugetSettings = Settings.LoadDefaultSettings(_workingDirectory); // 載入 NuGet 設定
        _currentFramework = GetCurrentNuGetFramework(); // 初始化當前框架
    }

    // 解析並下載 NuGet 套件及其相依性
    public async Task<(string script, IEnumerable<string> references)> ProcessNugetReferences(string scriptContent)
    {
        var nugetRegex = new Regex(@"#r\s+""nuget:\s*([^,]+),\s*([^""]+)""", RegexOptions.Multiline);
        var matches = nugetRegex.Matches(scriptContent);
        var references = new List<string>();
        var processedPackages = new HashSet<string>(); // 避免重複處理相同的套件

        if (matches.Count == 0)
        {
            return (scriptContent, references);
        }

        var logger = NullLogger.Instance;
        var cache = new SourceCacheContext();
        var sources = SettingsUtility.GetEnabledSources(_nugetSettings)
            .Select(s => Repository.Factory.GetCoreV3(s))
            .ToList();

        if (!sources.Any())
        {
            Console.WriteLine("No enabled NuGet sources found in configuration.");
            return (scriptContent, references);
        }

        foreach (Match match in matches)
        {
            string packageId = match.Groups[1].Value.Trim();
            string version = match.Groups[2].Value.Trim();
            var packageVersion = new NuGetVersion(version);
            var packageIdentity = new PackageIdentity(packageId, packageVersion);
            await ProcessPackageAndDependencies(packageIdentity, sources, logger, cache, references, processedPackages);
        }

        // 移除 #r "nuget: ..." 指令
        string processedScript = nugetRegex.Replace(scriptContent, "");
        // foreach(var refs in references)
        // {
        //     Console.WriteLine($"Reference: {refs}");
        // }
        return (processedScript, references);
    }

    // 遞迴處理套件及其相依性
    private async Task ProcessPackageAndDependencies(PackageIdentity package, List<SourceRepository> repositories, ILogger logger, SourceCacheContext cache, List<string> references, HashSet<string> processedPackages, bool checkDependencies = true)
    {
        if (processedPackages.Contains(package.Id))
        {
            return; // 已處理過，跳過
        }

        processedPackages.Add(package.Id);

        // 先檢查系統是否已有該套件
        string? systemPackagePath = FindSystemPackage(package.Id, package.Version);
        string? packagePath;

        if (systemPackagePath != null)
        {
            packagePath = systemPackagePath;
        }
        else
        {
            // 如果系統沒有且框架相容，則從可用來源下載
            packagePath = await DownloadFromSources(package, repositories, logger, cache);
            if (packagePath == null)
            {
                Console.WriteLine($"Package {package.Id} version {package.Version} not found in any source.");
                return;
            }
        }

        // 提取 DLL
        var dllFiles = ExtractDlls(packagePath);
        references.AddRange(dllFiles);

        if (!checkDependencies) return;
        // 檢查相依性
        using var packageReader = new PackageArchiveReader(packagePath);
        var dependencyGroups = packageReader.NuspecReader.GetDependencyGroups();
        var compatibleGroup = dependencyGroups
            .OrderByDescending(g => g.TargetFramework.Version) // 選擇最高版本的框架
            .FirstOrDefault(g => IsFrameworkCompatible(_currentFramework, g.TargetFramework))
            ?? dependencyGroups.FirstOrDefault(); // 如果沒有相容的，選擇第一個

        if (compatibleGroup != null)
        {
            foreach (var dependency in compatibleGroup.Packages)
            {
                var dependencyVersion = dependency.VersionRange.MinVersion ?? dependency.VersionRange.MaxVersion;
                if (dependencyVersion == null)
                {
                    Console.WriteLine($"Unable to determine version for dependency {dependency.Id} of {package.Id}");
                    continue;
                }

                var dependencyIdentity = new PackageIdentity(dependency.Id, dependencyVersion);
                await ProcessPackageAndDependencies(dependencyIdentity, repositories, logger, cache, references, processedPackages, true);
            }
        }
    }

    // 自訂框架相容性檢查
    private bool IsFrameworkCompatible(NuGetFramework current, NuGetFramework target)
    {
        if (target.IsAny || target.IsAgnostic) return true; // 任何框架或框架不可知都相容

        var reducer = new FrameworkReducer();
        var nearest = reducer.GetNearest(current, new[] { target });
        return nearest != null; // 如果找到最近的相容框架，則認為相容
    }

    // 從多個來源下載套件
    private async Task<string?> DownloadFromSources(PackageIdentity package, List<SourceRepository> repositories, ILogger logger, SourceCacheContext cache)
    {
        foreach (var repository in repositories)
        {
            var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            if (await resource.DoesPackageExistAsync(package.Id, package.Version, cache, logger, CancellationToken.None))
            {
                return await downloadPackage(resource, package.Id, package.Version, logger, cache);
            }
        }
        return null; // 未在任何來源找到
    }

    // 檢查系統是否已有該 NuGet 套件
    private string? FindSystemPackage(string packageId, NuGetVersion version)
    {
        var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(_nugetSettings);
        string packageFileName = $"{packageId}.{version}.nupkg".ToLowerInvariant();
        string potentialPath = Path.Combine(globalPackagesFolder, packageId.ToLowerInvariant(), version.ToString(), packageFileName);

        if (File.Exists(potentialPath))
        {
            return potentialPath;
        }

        // 檢查本地緩存目錄
        string localPath = Path.Combine(NugetPackageDir, packageFileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return null;
    }

    // 下載 NuGet 套件到本地
    private async Task<string> downloadPackage(FindPackageByIdResource resource, string packageId, NuGetVersion version, ILogger logger, SourceCacheContext cache)
    {
        string packagesDir = NugetPackageDir;

        string packageFile = Path.Combine(packagesDir, $"{packageId}.{version}.nupkg");
        if (!File.Exists(packageFile))
        {
            Directory.CreateDirectory(packagesDir);
            using var packageStream = File.Create(packageFile);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 設定 30 秒超時
            await resource.CopyNupkgToStreamAsync(packageId, version, packageStream, cache, logger, cts.Token);
        }
        return packageFile;
    }

    public (string script, IEnumerable<string> references) ProcessDllReference(string csxDir, string striptContent)
    {
        var dllRegex = new Regex(@"#r\s+""([^""]+.dll)""", RegexOptions.Multiline);
        var matches = dllRegex.Matches(striptContent);
        var references = new List<string>();

        if (matches.Count == 0)
        {
            return (striptContent, references);
        }

        foreach (Match match in matches)
        {
            string dllPath = match.Groups[1].Value.Trim();
            var dllDir = Path.Combine(csxDir, dllPath);
            if (File.Exists(dllDir))
            {
                references.Add(dllPath);
            }
        }

        // 移除 #r "..." 指令
        string processedScript = dllRegex.Replace(striptContent, "");
        return (processedScript, references);
    }

    private static IEnumerable<dynamic> GetDllsInfo(IEnumerable<string> packagePaths, string currentRuntime)
    {
        var libFiles = 
            packagePaths.Where(i => i.EndsWith(".dll"))
            .Select(i => new 
            { 
                Path = i, 
                FileName = Path.GetFileName(i),
                Framework = GetTargetFramework(i),  // 提取目標框架
                Culture = GetCulture(i),             // 提取語系（如果有）
                Platform = GetPlatform(i)    // 提取平台（如果有）
            });

        // 有 runtime 用 runttime 沒有 runtime 用 lib

        var result = libFiles
            .Where(f => IsCompatibleFramework(f.FileName, f.Framework.tfm, currentRuntime))
            .OrderBy(f => DotnetDirOrder(f.Framework.dir, f.Platform)) // 優先選擇 runtime 目錄下的 lib
            .ThenByDescending(f => DotnetFrameworkOrder(f.Framework.tfm)) // 優先選擇較新的框架
            .ThenByDescending(f => f.Framework.tfm) // 再按框架名稱排序
            .GroupBy(f => new { f.FileName, f.Framework.dir })
            .SelectMany(g => 
                !string.IsNullOrEmpty(g.First().Culture) 
                    ? g // 如果 Culture 是空字串，返回整個分組
                    : g.Take(1) // 否則只取第一個
            )
            .Select(f => new { Path = f.Path, FileName = f.FileName, Framework = f.Framework.tfm, Dir = f.Framework.dir, Platform = f.Platform, Culture = f.Culture });

        // Console.WriteLine("-------------------------------------");
        // foreach(var s in result)
        // {
        //     Console.WriteLine($"Path: {s.Path}, FileName: {s.FileName}, Framework: {s.Framework}, Dir: {s.Dir}, Culture: {s.Culture}");
        // }
        // Console.WriteLine("=====================================");
        return result;
    }
    public static int DotnetDirOrder(string dir, string platform)
    {
        if (dir == PackagingConstants.Folders.Runtimes)
        {
            if (_isWindows && platform == "win") return 1;
            if (!_isWindows && platform != "win") return 1;
            return 2;
        }
        return 2;
    }
    public static int DotnetFrameworkOrder(string tfm)
    {
        if (tfm.StartsWith("netstandard"))
        {
            return 3;
        }
        if (tfm.StartsWith("netcoreapp"))
        {
            return 2;
        }
        if (tfm.StartsWith("net"))
        {
            return 4;
        }
        return 1;
    }

    public static IEnumerable<string> ExtractDlls(string packagePath)
    {
        var directoryName = Path.GetDirectoryName(packagePath) 
            ?? throw new ArgumentNullException(nameof(packagePath), "Package path directory name is null");
        var extractDir = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(packagePath));
        
        string currentRuntime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.TrimStart('.').Replace(" ", "").ToLower();
        string currentCulture = Thread.CurrentThread.CurrentUICulture.Name;

        if (!Directory.Exists(extractDir))
        {
            using var packageReader = new PackageArchiveReader(packagePath);
            var compatibleFiles = GetDllsInfo(packageReader.GetLibItems()
                .Concat(packageReader.GetItems(PackagingConstants.Folders.Runtimes))
                .SelectMany(g => g.Items), currentRuntime);
            if (compatibleFiles.Any())
            {
                Directory.CreateDirectory(extractDir);
            }
            foreach (var file in compatibleFiles)
            {
                var relativePath = file.Path.Replace('/', Path.DirectorySeparatorChar);
                var destPath = Path.Combine(extractDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath) ?? throw new ArgumentNullException(nameof(destPath), "Destination path directory name is null");
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                packageReader.ExtractFile(file.Path, destPath, NullLogger.Instance);

                if (compatibleFiles.Any(x => x.FileName == file.FileName && x.Dir == PackagingConstants.Folders.Runtimes)) {
                    if (file.Dir != PackagingConstants.Folders.Runtimes) continue;
                    if (file.Dir == PackagingConstants.Folders.Runtimes) {
                        if (_isWindows && file.Platform != "win") continue;
                        if (!_isWindows && file.Platform == "win") continue;
                    }
                }
                if (file.Culture != "") continue;
                yield return destPath;
            }
        }
        else
        {
            var libFiles = Directory.GetFiles(extractDir, "*.dll", SearchOption.AllDirectories).Select(x => x.Replace(extractDir, "").TrimStart('/').TrimStart('\\'));
            var compatibleFiles = GetDllsInfo(libFiles, currentRuntime);

            foreach (var file in compatibleFiles)
            {
                if (compatibleFiles.Any(x => x.FileName == file.FileName && x.Dir == PackagingConstants.Folders.Runtimes)) {
                    if (file.Dir != PackagingConstants.Folders.Runtimes) continue;
                    if (file.Dir == PackagingConstants.Folders.Runtimes) {
                        if (_isWindows && file.Platform != "win") continue;
                        if (!_isWindows && file.Platform == "win") continue;
                    }
                }
                if (file.Culture != "") continue;
                var relativePath = file.Path.Replace('/', Path.DirectorySeparatorChar);
                var destPath = Path.Combine(extractDir, relativePath);
                yield return destPath;
            }
        }
    }

    private static string GetPlatform(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        if (parts[0] == PackagingConstants.Folders.Runtimes)
        {
            return parts[1];
        }
        return string.Empty;
    }

    private static string GetCulture(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        if (parts[0] == PackagingConstants.Folders.Runtimes)
        {
            return parts.Length > 5 ? parts[4] : string.Empty;
        }
        if (parts.Length > 3)
        {
            return parts[2];
        }
        return string.Empty;
    }

    private static (string dir, string tfm) GetTargetFramework(string path)
    {
        (string dir, string tfm) result = (string.Empty, string.Empty);
        var parts = path.Split(Path.DirectorySeparatorChar);
        var libIndex = Array.IndexOf(parts, PackagingConstants.Folders.Lib);
        var runtimeIndex = Array.IndexOf(parts, PackagingConstants.Folders.Runtimes); // 先檢查如果有 runtime 的話，要以 runtime 目錄下的 lib 為主
        if (runtimeIndex >= 0 && runtimeIndex + 1 < parts.Length)
        {
            result.dir = PackagingConstants.Folders.Runtimes;
            result.tfm = (parts[libIndex + 1]).ToLower();
            return result;
        }
        if (libIndex >= 0 && libIndex + 1 < parts.Length)
        {
            result.dir = PackagingConstants.Folders.Lib;
            result.tfm = (parts[libIndex + 1]).ToLower();
            return result;
        }
        return result;
    }

    private static bool IsCompatibleFramework(string name, string framework, string currentRuntime)
    {
        if (string.IsNullOrEmpty(framework)) return true;
        if (currentRuntime.Contains(framework.Split('.')[0])) return true;
        
        var reducer = new FrameworkReducer();
        var nearest = reducer.GetNearest(NuGetFramework.Parse(currentRuntime), new[] { NuGetFramework.Parse(framework) });
        return nearest != null; // 如果找到最近的相容框架，則認為相容
    }

    private static NuGetFramework GetCurrentNuGetFramework()
    {
        var frameworkName = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var match = Regex.Match(frameworkName, @"(\.NET Core|\.NET) (\d+\.\d+)");
        if (match.Success)
        {
            string prefix = match.Groups[1].Value == ".NET Core" ? "netcoreapp" : "net";
            string version = match.Groups[2].Value;
            return NuGetFramework.Parse($"{prefix}{version}");
        }
        return NuGetFramework.Parse("net8.0");
    }
    public void ShowNugetPackages()
    {
        if (!Directory.Exists(NugetPackageDir)) return;
        Console.WriteLine();

        foreach (var dir in Directory.GetDirectories(NugetPackageDir))
        {
            Console.WriteLine(Path.GetFileName(dir));
        }
        foreach (var nupkg in Directory.GetFiles(NugetPackageDir, "*.nupkg", SearchOption.TopDirectoryOnly))
        {
            Console.WriteLine(Path.GetFileName(nupkg));
        }
        foreach (var dll in Directory.GetFiles(NugetPackageDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            Console.WriteLine(Path.GetFileName(dll));
        }
    }

    public (int delFiles, int delDirs) CleanNugetPackages(string nugetName = "")
    {
        (int delFiles, int delDirs) result = (0, 0);
        if (!Directory.Exists(NugetPackageDir)) return result;

        Console.WriteLine();

        var hasExtension = Path.GetExtension(nugetName) != "";
        string nupkgPattern = string.IsNullOrEmpty(nugetName) ? "*.nupkg" : $"{nugetName}{(hasExtension?"":".nupkg")}";
        foreach (var nupkg in Directory.GetFiles(NugetPackageDir, nupkgPattern, SearchOption.TopDirectoryOnly))
        {
            result.delFiles++;
            Console.WriteLine($"Delete {nupkg}");
            File.Delete(nupkg);
        }

        string dllPattern = string.IsNullOrEmpty(nugetName) ? "*.dll" : $"{nugetName}{(hasExtension?"":".dll")}";
        foreach (var dll in Directory.GetFiles(NugetPackageDir, dllPattern, SearchOption.TopDirectoryOnly))
        {
            result.delFiles++;
            Console.WriteLine($"Delete {dll}");
            File.Delete(dll);
        }
        var toberemoveDirs = (nugetName == "") ? Directory.GetDirectories(NugetPackageDir) : Directory.GetDirectories(NugetPackageDir).Where(d => Path.GetFileName(d) == nugetName);
        foreach (var dir in toberemoveDirs)
        {
            result.delDirs++;
            Console.WriteLine($"Delete {dir}");
            Directory.Delete(dir, true);
        }
        return result;
    }

    public static IEnumerable<string> GetImports(string scriptContent)
    {
        var importRegex = new Regex(@"using\s+([^;]+);", RegexOptions.Multiline);
        return importRegex.Matches(scriptContent).Select(m => m.Groups[1].Value.Trim());
    }
}
