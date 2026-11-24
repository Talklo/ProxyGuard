using System.Threading;

namespace ProxyGuard;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0].Equals("--launch", StringComparison.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            LauncherMode.RunAsync(args[1]).GetAwaiter().GetResult();
            return;
        }

        using var singleInstance = new Mutex(true, "Local\\ProxyGuard.Singleton", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("代理守护已经在运行。", "代理守护",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new GuardApplicationContext());
    }
}
