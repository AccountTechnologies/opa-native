using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Opa.Native;

public class Opa
{
    public OpaServerHandle RunServer() => Execute(psi =>
    {
        psi.Arguments = "run --server --format json-pretty --log-level debug";
    });

    public OpaServerHandle Execute(Action<ProcessStartInfo> configure)
    {
        static string GetOpaBinaryPath()
        {
            var asmLocation = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (asmLocation is null) throw new InvalidOperationException("Directory name is null");
            static bool IsOs(OSPlatform p) => RuntimeInformation.IsOSPlatform(p);
            var osSpecificDir =
                IsOs(OSPlatform.Windows) ? "runtimes/win-x64/native/opa_windows_amd64.exe" :
                IsOs(OSPlatform.Linux) ? "runtimes/linux-x64/native/opa_linux_amd64" :
                IsOs(OSPlatform.OSX) ? "runtimes/osx-x64/native/opa_darwin_amd64" :
                throw new NotSupportedException($"Os platform '{RuntimeInformation.OSDescription}' is not supported");
            return System.IO.Path.GetFullPath(osSpecificDir, asmLocation);
        }
        var cts = new CancellationTokenSource();
        var psi = new ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = GetOpaBinaryPath()
        };
        configure(psi);
        var p = Process.Start(psi);
        if (p is null) throw new InvalidOperationException("Opa server failed starting");
        if (psi.RedirectStandardOutput) p.BeginOutputReadLine();
        if (psi.RedirectStandardError) p.BeginErrorReadLine();
        var tcs = new TaskCompletionSource<int>();
        cts.Token.Register(() =>
        {
            try
            {
                p.Kill();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            tcs.SetCanceled();
        });

        p.Exited += (sender, args) =>
        {
            if (sender is Process x) tcs.TrySetResult(x.ExitCode);
        };
        return new OpaServerHandle(tcs.Task, cts);
    }
}
