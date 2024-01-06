using System;
using System.Xml.Linq;
using System.Xml.XPath;
using Cesium.CodeGen;
using Xunit.Abstractions;

namespace Cesium.Test.Framework;

// TODO[#492]: Make a normal disposable class to delete the whole directory in the end of the test.
public class CSharpCompilationUtil : IDisposable
{
    public static readonly TargetRuntimeDescriptor DefaultRuntime = TargetRuntimeDescriptor.Net60;
    private const string _configuration = "Debug";
    private const string _targetRuntime = "net7.0";
    private const string _cesiumRuntimeLibTargetRuntime = "net6.0";
    private const string _projectName = "TestProject";

    /// <summary>Semaphore that controls the amount of simultaneously running tests.</summary>
    private static readonly SemaphoreSlim _testSemaphore = new(Environment.ProcessorCount);

    // member variables so each instance of the Compilation Util only executes one source
    private string testDirectory;
    private ITestOutputHelper output;
    private TargetRuntimeDescriptor runtime;
    private string cSharpSource;


    public CSharpCompilationUtil(
            ITestOutputHelper outputIn,
            TargetRuntimeDescriptor runtimeIn,
            string cSharpSourceIn) 
    {
        testDirectory = Path.GetTempFileName(); 
        File.Delete(testDirectory);  
        Directory.CreateDirectory(testDirectory);

        output = outputIn;
        runtime = runtimeIn;
        cSharpSource = cSharpSourceIn;

    }

    public async Task<string> CompileCSharpAssembly()
    {
        if (runtime != DefaultRuntime) throw new Exception($"Runtime {runtime} not supported for test compilation.");
        await _testSemaphore.WaitAsync();
        try
        {
            var projectDirectory = await CreateCSharpProject(output, testDirectory);
            await File.WriteAllTextAsync(Path.Combine(projectDirectory, "Program.cs"), cSharpSource);
            await CompileCSharpProject(output, testDirectory, _projectName);
            return Path.Combine(projectDirectory, "bin", _configuration, _targetRuntime, _projectName + ".dll");
        }
        finally
        {
            _testSemaphore.Release();
        }
    } 

    private async Task<string> CreateCSharpProject(ITestOutputHelper output, string directory)
    {
        await ExecUtil.RunToSuccess(
            output,
            "dotnet",
            directory,
            new[] { "new", "classlib", "--framework", _targetRuntime, "--output", _projectName });
        var projectDirectory = Path.Combine(directory, _projectName);
        var projectFilePath = Path.Combine(projectDirectory, $"{_projectName}.csproj");
        XDocument csProj;
        await using (var projectFileStream = new FileStream(projectFilePath, FileMode.Open, FileAccess.Read))
        {
            csProj = await XDocument.LoadAsync(projectFileStream, LoadOptions.None, CancellationToken.None);
        }

        var project = csProj.XPathSelectElement("/Project")!;
        project.Add(new XElement("PropertyGroup",
            new XElement(new XElement("AllowUnsafeBlocks", "true"))));

        project.Add(new XElement("ItemGroup",
            new XElement("Reference", new XAttribute("Include", CesiumRuntimeLibraryPath))));

        await using var outputStream = new FileStream(projectFilePath, FileMode.Truncate, FileAccess.Write);
        await csProj.SaveAsync(outputStream, SaveOptions.None, CancellationToken.None);

        return projectDirectory;
    }

    public static readonly string CesiumRuntimeLibraryPath = Path.Combine(
        TestStructureUtil.SolutionRootPath,
        "Cesium.Runtime",
        "bin",
        _configuration,
        _cesiumRuntimeLibTargetRuntime,
        "Cesium.Runtime.dll");

    private Task CompileCSharpProject(ITestOutputHelper output, string directory, string projectName) =>
        ExecUtil.RunToSuccess(output, "dotnet", directory, new[]
        {
            "build",
            projectName,
            "--configuration", _configuration,
        });
    
    public void Dispose() {
        Directory.Delete(testDirectory, true);
    }
}
