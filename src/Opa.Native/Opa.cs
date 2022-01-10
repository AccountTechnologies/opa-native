using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Opa.Native;

public static class OpaProcess
{
    public static async Task<OpaHandle> StartServerAsync()
    {
        var tcs = new TaskCompletionSource<OpaHandle>();
        Execute(psi =>
        {
            psi.Arguments = "run --server --format json-pretty --log-level debug";
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
        }, (p, h) =>
        {
            void WaitForServerUp(object sender, DataReceivedEventArgs args)
            {
                if (args.Data is null) return;
                Debug.WriteLine(args.Data);
                if (args.Data.Contains("Server initialized."))
                {
                    p.ErrorDataReceived -= WaitForServerUp;
                    tcs.SetResult(h);
                }
            }
            p.ErrorDataReceived += WaitForServerUp;
        });

        return await tcs.Task;
    }

    public static OpaHandle Execute(Action<ProcessStartInfo> configure, Action<Process, OpaHandle>? onProcessStarted = null)
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
        var tcs = new TaskCompletionSource<int>();
        var psi = new ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = GetOpaBinaryPath()
        };
        configure(psi);
        var p = Process.Start(psi);
        if (p is null) throw new InvalidOperationException("Opa server failed starting");
        var handle = new OpaHandle(tcs.Task, cts);
        onProcessStarted?.Invoke(p, handle);
        if (psi.RedirectStandardOutput) p.BeginOutputReadLine();
        if (psi.RedirectStandardError) p.BeginErrorReadLine();

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
        return handle;
    }
}
