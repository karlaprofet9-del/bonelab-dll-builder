using System.IO.Compression;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(o => {
    o.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});
var app = builder.Build();

var root = Path.Combine(Path.GetTempPath(), "bonelab-builder");
Directory.CreateDirectory(root);

app.MapGet("/", () => Results.Content("""
<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1">
<title>BONELAB DLL Builder</title>
<style>
body{font-family:system-ui;max-width:680px;margin:35px auto;padding:0 16px;background:#111827;color:#f9fafb}
.card{background:#1f2937;padding:20px;border-radius:14px;margin:16px 0}
label{display:block;margin:14px 0 6px}input,select,button{width:100%;box-sizing:border-box;padding:13px;border-radius:9px;border:0}
button{background:#22c55e;color:#052e16;font-weight:700;margin-top:18px;font-size:16px}
small{color:#cbd5e1}h1{font-size:26px}#status{white-space:pre-wrap;margin-top:16px}
</style></head><body>
<h1>🛠️ BONELAB DLL Builder</h1>
<p>Upload your C# mod and the DLL references you want the compiler to use.</p>
<form id="form" class="card">
<label>C# mod files (.cs)</label><input name="sources" type="file" accept=".cs" multiple required>
<label>Reference DLL files (.dll)</label><input name="references" type="file" accept=".dll" multiple>
<label>Target framework</label><select name="framework"><option>netstandard2.1</option><option>netstandard2.0</option><option>net6.0</option></select>
<small>Choose the framework required by your specific mod-loader setup. If unsure, check the loader's documentation.</small>
<button type="submit">BUILD DLL</button>
<div id="status"></div>
</form>
<script>
document.querySelector('#form').addEventListener('submit',async e=>{
 e.preventDefault();const s=document.querySelector('#status');s.textContent='Building…';
 try{const r=await fetch('/build',{method:'POST',body:new FormData(e.target)});
 if(!r.ok){s.textContent=await r.text();return}
 const b=await r.blob(),u=URL.createObjectURL(b),a=document.createElement('a');
 a.href=u;a.download='BONELABMod.dll';a.click();URL.revokeObjectURL(u);s.textContent='Build complete. DLL download started.';
 }catch(err){s.textContent='Build failed: '+err}
});
</script></body></html>
""", "text/html"));

app.MapPost("/build", async (HttpRequest request) => {
    var form = await request.ReadFormAsync();
    var sources = form.Files.Where(f => f.Name == "sources" && Path.GetExtension(f.FileName).Equals(".cs", StringComparison.OrdinalIgnoreCase)).ToList();
    var refs = form.Files.Where(f => f.Name == "references" && Path.GetExtension(f.FileName).Equals(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
    if (sources.Count == 0) return Results.BadRequest("Upload at least one .cs file.");
    var framework = form["framework"].ToString();
    if (framework is not ("netstandard2.1" or "netstandard2.0" or "net6.0")) return Results.BadRequest("Unsupported target framework.");

    var id = Guid.NewGuid().ToString("N");
    var work = Path.Combine(root, id);
    var srcDir = Path.Combine(work, "src");
    var refDir = Path.Combine(work, "refs");
    var outDir = Path.Combine(work, "out");
    Directory.CreateDirectory(srcDir); Directory.CreateDirectory(refDir); Directory.CreateDirectory(outDir);

    try {
        foreach (var f in sources) {
            var name = Path.GetFileName(f.FileName);
            await using var fs = File.Create(Path.Combine(srcDir, name));
            await f.CopyToAsync(fs);
        }
        foreach (var f in refs) {
            var name = Path.GetFileName(f.FileName);
            await using var fs = File.Create(Path.Combine(refDir, name));
            await f.CopyToAsync(fs);
        }

        var referenceItems = string.Join("\n", Directory.GetFiles(refDir, "*.dll").Select(p =>
            $"    <Reference Include=\"{System.Security.SecurityElement.Escape(Path.GetFileNameWithoutExtension(p))}\"><HintPath>../refs/{System.Security.SecurityElement.Escape(Path.GetFileName(p))}</HintPath><Private>false</Private></Reference>"));
        var csproj = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>{framework}</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <AssemblyName>BONELABMod</AssemblyName>
    <OutputPath>../out/</OutputPath>
  </PropertyGroup>
  <ItemGroup>
{referenceItems}
  </ItemGroup>
</Project>
""";
        await File.WriteAllTextAsync(Path.Combine(work, "src", "Mod.csproj"), csproj);
        var psi = new ProcessStartInfo("dotnet", "build Mod.csproj --configuration Release --nologo") {
            WorkingDirectory = srcDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var output = await stdoutTask + await stderrTask;
        if (proc.ExitCode != 0) return Results.BadRequest("Build failed:\n" + output);
        var dll = Path.Combine(outDir, "BONELABMod.dll");
        if (!File.Exists(dll)) return Results.BadRequest("Build finished but output DLL was not found.\n" + output);
        return Results.File(dll, "application/octet-stream", "BONELABMod.dll");
    } finally {
        try { Directory.Delete(work, true); } catch {}
    }
});
app.Run();
