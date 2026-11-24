using ProxyGuard;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var failures = new List<string>();
        foreach (var scenario in new[]
                 {
                     (Width: 800, Height: 650, FontSize: 9f, Name: "800x650@100%"),
                     (Width: 800, Height: 650, FontSize: 11.25f, Name: "800x650@125%"),
                     (Width: 960, Height: 720, FontSize: 13.5f, Name: "960x720@150%")
                 })
        {
            using var form = new SettingsForm(CreateConfig());
            form.Font = new Font("Microsoft YaHei UI", scenario.FontSize);
            form.Size = new Size(scenario.Width, scenario.Height);
            form.CreateControl();
            var tabs = FindControls<TabControl>(form).First();
            foreach (TabPage page in tabs.TabPages)
            {
                tabs.SelectedTab = page;
                PerformLayoutRecursively(form);
                CheckControls(form, $"{scenario.Name}/{page.Text}", failures);
            }

            using var targetForm = new TargetAppForm(CreateConfig().Apps[0]);
            targetForm.Font = new Font("Microsoft YaHei UI", scenario.FontSize);
            targetForm.CreateControl();
            PerformLayoutRecursively(targetForm);
            CheckControls(targetForm, $"{scenario.Name}/受保护软件编辑", failures);
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("PASS: settings layout fits all tested window and font sizes.");
            return 0;
        }

        Console.WriteLine($"FAIL: {failures.Count} layout issue(s)");
        foreach (var failure in failures.Take(30)) Console.WriteLine(failure);
        return 1;
    }

    private static GuardConfig CreateConfig() => new()
    {
        ProxyClientType = "其他自定义客户端",
        ProxyClientExecutablePath = @"C:\Program Files\A Very Long Proxy Client Name\ProxyClient.exe",
        RequireProxyClientProcess = true,
        UseClashController = true,
        ControllerUrl = "http://127.0.0.1:9090",
        ControllerSecret = "test-secret",
        ProxyUrl = "socks5://127.0.0.1:1080",
        ProbeUrl = "https://www.gstatic.com/generate_204",
        LatencyProxyName = "A long policy group name for layout testing",
        MaxLatencyMilliseconds = 1000,
        Apps =
        [
            new TargetApp
            {
                Name = "Protected Application With A Long Name",
                ExecutablePath = @"C:\Program Files\Protected Application\ProtectedApplication.exe"
            }
        ]
    };

    private static void PerformLayoutRecursively(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) PerformLayoutRecursively(child);
    }

    private static void CheckControls(Control parent, string scenario, List<string> failures)
    {
        foreach (Control child in parent.Controls)
        {
            var viewport = parent.DisplayRectangle;
            if (!viewport.Contains(child.Bounds) && parent is not ScrollableControl { AutoScroll: true })
                failures.Add($"[{scenario}] OUTSIDE {Describe(child)} bounds={child.Bounds} parent={parent.GetType().Name} viewport={viewport}");

            if (child is Label { AutoSize: false } wrappedLabel)
            {
                var measured = TextRenderer.MeasureText(wrappedLabel.Text, wrappedLabel.Font,
                    new Size(Math.Max(1, wrappedLabel.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                if (child.ClientSize.Height + 1 < measured.Height)
                    failures.Add($"[{scenario}] CLIPPED {Describe(child)} size={child.ClientSize} wrapped={measured}");
            }
            else if (child is Label or CheckBox or Button)
            {
                var preferred = child.GetPreferredSize(Size.Empty);
                if (child.ClientSize.Width + 1 < preferred.Width || child.ClientSize.Height + 1 < preferred.Height)
                    failures.Add($"[{scenario}] CLIPPED {Describe(child)} size={child.ClientSize} preferred={preferred}");
            }
            CheckControls(child, scenario, failures);
        }
    }

    private static IEnumerable<T> FindControls<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match) yield return match;
            foreach (var descendant in FindControls<T>(child)) yield return descendant;
        }
    }

    private static string Describe(Control control)
    {
        var text = control.Text.Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 45) text = text[..45];
        return $"{control.GetType().Name} '{text}'";
    }
}
