using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace NaturalStoneImpex.Api.Tests;

public class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "nsi-tests-wwwroot");
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "NaturalStoneImpex.Api";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Development";
}
