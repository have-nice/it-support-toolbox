using System.Diagnostics;

namespace ItSupportToolbox;

public sealed class MainForm : Form
{
    private static readonly (string Key, string Glyph, string Label)[] NavItems =
    {
        ("network", "\uE774", "Network"),
        ("system", "\uE713", "System"),
        ("security", "\uE72E", "Security"),
        ("report", "\uE8A5", "Report"),
        ("about", "\uE946", "About")
    };

    private static readonly Dictionary<string, (string Title, string Subtitle)> PageInfo = new()
    {
        ["network"] = ("Network & connectivity", "Diagnose and repair DNS, DHCP, routing and adapter problems."),
        ["system"] = ("System & hardware", "Inspect specifications, disks, services, startup items and event history."),
        ["security"] = ("Security & firewall", "Check Defender, firewall profiles, proxies, network profile and BitLocker."),
        ["report"] = ("Diagnostic report", "Export or copy everything captured during this session."),
        ["about"] = ("About", "Portable diagnostics built for quick on-site IT support.")
    };

    private readonly bool _isAdmin;
    private readonly Shell _shell = new();
    private readonly Diagnostics _diagnostics;
    private readonly ReportStore _report = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Panel> _pages = new();
    private readonly List<NavButton> _navButtons = new();

    private Panel _pageHost = null!;
    private Label _pageTitle = null!;
    private Label _pageSubtitle = null!;
    private Label _statusLabel = null!;
    private Label _reportCount = null!;
    private SplitContainer _split = null!;
    private OutputConsole _output = null!;
    private string _currentPage = "network";

    public MainForm(bool isAdmin)
    {
        _isAdmin = isAdmin;
        _diagnostics = new Diagnostics(_shell);

        Text = "IT Support Toolbox";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1060, 720);
        ClientSize = new Size(1240, 820);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Ui(9.5f);
        DoubleBuffered = true;

        BuildUi();

        Load += (_, _) =>
        {
            try
            {
                int height = _split.Height;
                int target = Math.Max(240, height - 300);
                _split.SplitterDistance = Math.Min(target, Math.Max(240, height - 200));
                _split.Panel1MinSize = 230;
                _split.Panel2MinSize = 170;
            }
            catch
            {
                // layout not ready; keep default split
            }
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ApplyDarkTitleBar(this);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(root);

        BuildPages();

        _report.Changed += UpdateReportCount;
        UpdateReportCount();
        ShowPage("network");
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Sidebar,
            Padding = new Padding(14, 18, 14, 14),
            Margin = Padding.Empty
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        sidebar.Controls.Add(BuildLogo(), 0, 0);

        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = NavItems.Length + 1,
            BackColor = Theme.Sidebar,
            Padding = new Padding(0, 26, 0, 0),
            Margin = Padding.Empty
        };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        for (int i = 0; i < NavItems.Length; i++)
        {
            nav.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var item = NavItems[i];
            var button = new NavButton(item.Glyph, item.Label) { Tag = item.Key };
            button.Click += (_, _) => ShowPage(item.Key);
            nav.Controls.Add(button, 0, i);
            _navButtons.Add(button);
        }
        nav.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        sidebar.Controls.Add(nav, 0, 1);
        sidebar.Controls.Add(BuildSidebarFooter(), 0, 2);
        return sidebar;
    }

    private static Control BuildLogo()
    {
        var logo = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Sidebar,
            Margin = Padding.Empty
        };
        logo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
        logo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        logo.Controls.Add(new LogoMark { Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 10, 0) }, 0, 0);

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Sidebar,
            Margin = Padding.Empty
        };
        titles.Controls.Add(new Label
        {
            Text = "IT SUPPORT",
            Font = Theme.Ui(12f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Theme.Sidebar,
            AutoSize = true,
            Margin = Padding.Empty
        }, 0, 0);
        titles.Controls.Add(new Label
        {
            Text = "T O O L B O X",
            Font = Theme.Ui(7.5f, FontStyle.Bold),
            ForeColor = Theme.Accent,
            BackColor = Theme.Sidebar,
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 0)
        }, 0, 1);

        logo.Controls.Add(titles, 1, 0);
        return logo;
    }

    private Control BuildSidebarFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Sidebar,
            Margin = Padding.Empty,
            Padding = new Padding(0, 12, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _statusLabel = new Label
        {
            Text = "Ready",
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.Muted,
            BackColor = Theme.Sidebar,
            AutoSize = true,
            MaximumSize = new Size(200, 0),
            Margin = new Padding(2, 0, 0, 10)
        };
        footer.Controls.Add(_statusLabel, 0, 0);

        var statusRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = Theme.Sidebar,
            Margin = new Padding(0, 0, 0, 8)
        };
        statusRow.Controls.Add(new Pill(_isAdmin ? "ADMINISTRATOR" : "STANDARD USER",
            _isAdmin ? Theme.Green : Theme.Amber));

        if (!_isAdmin)
        {
            var elevate = new AccentButton("Elevate", ButtonKind.Ghost, 84, 26, 8.5f)
            {
                Margin = new Padding(0, 0, 0, 4)
            };
            elevate.Click += (_, _) =>
            {
                var args = Environment.GetCommandLineArgs().Skip(1)
                    .Where(a => !a.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (Elevation.TryRelaunchElevated(args))
                    Close();
                else
                    ModalDialog.Info(this, "Elevation cancelled",
                        "The app is still running as a standard user. Some repair actions may fail.");
            };
            statusRow.Controls.Add(elevate);
        }
        footer.Controls.Add(statusRow, 0, 1);

        footer.Controls.Add(new Label
        {
            Text = "v1.0.0  \u00B7  portable",
            Font = Theme.Ui(8f),
            ForeColor = Theme.Faint,
            BackColor = Theme.Sidebar,
            AutoSize = true,
            Margin = new Padding(2, 4, 0, 0)
        }, 0, 2);

        return footer;
    }

    private Control BuildMainArea()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        main.Controls.Add(BuildHeader(), 0, 0);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 10,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _split.Panel1.BackColor = Theme.Bg;
        _split.Panel2.BackColor = Theme.Bg;
        _split.Panel2.Padding = new Padding(24, 0, 24, 18);

        _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        _split.Panel1.Controls.Add(_pageHost);

        _output = new OutputConsole { Dock = DockStyle.Fill };
        _output.SaveRequested += () => ExportReport(html: false);
        _output.ClearRequested += () => _output.ClearOutput();
        _split.Panel2.Controls.Add(_output);

        main.Controls.Add(_split, 0, 1);
        return main;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Bg,
            Padding = new Padding(24, 16, 24, 10),
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Bg,
            Margin = Padding.Empty
        };
        titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _pageTitle = new Label
        {
            Text = "Network & connectivity",
            Font = Theme.Ui(17f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Theme.Bg,
            AutoSize = true,
            UseMnemonic = false,
            Margin = Padding.Empty
        };
        _pageSubtitle = new Label
        {
            Text = PageInfo["network"].Subtitle,
            Font = Theme.Ui(9.25f),
            ForeColor = Theme.Muted,
            BackColor = Theme.Bg,
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        titles.Controls.Add(_pageTitle, 0, 0);
        titles.Controls.Add(_pageSubtitle, 0, 1);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = Theme.Bg,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty
        };

        var runAll = new AccentButton("Run all checks", ButtonKind.Accent, 146, 34)
        {
            Margin = new Padding(0, 2, 0, 0)
        };
        runAll.Click += async (_, _) => await RunAllChecksAsync(runAll);
        right.Controls.Add(runAll);

        right.Controls.Add(new Pill(_isAdmin ? "ADMINISTRATOR" : "STANDARD USER",
            _isAdmin ? Theme.Green : Theme.Amber));

        right.Controls.Add(new Label
        {
            Text = Environment.MachineName,
            Font = Theme.Ui(9f, FontStyle.Bold),
            ForeColor = Theme.Muted,
            BackColor = Theme.Bg,
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 0)
        });

        header.Controls.Add(titles, 0, 0);
        header.Controls.Add(right, 1, 0);
        return header;
    }

    private void BuildPages()
    {
        AddPage("network", BuildNetworkPage());
        AddPage("system", BuildSystemPage());
        AddPage("security", BuildSecurityPage());
        AddPage("report", BuildReportPage());
        AddPage("about", BuildAboutPage());
    }

    private void AddPage(string key, Panel page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages[key] = page;
        _pageHost.Controls.Add(page);
    }

    private static Panel NewPage(params Card[] cards)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            AutoScroll = true,
            Padding = new Padding(24, 16, 24, 16)
        };
        host.HandleCreated += (_, _) => Native.ApplyDarkScrollbars(host);

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        host.Controls.Add(stack);

        foreach (var card in cards)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Dock = DockStyle.Top;
            stack.Controls.Add(card);
        }

        return host;
    }

    private Panel BuildNetworkPage()
    {
        var dnsHost = new RoundedInput(236, "www.google.com");
        var traceHost = new RoundedInput(236, "www.google.com");

        var quick = new Card("Quick network fixes",
            "Flush caches, refresh the DHCP lease, verify internet access and test DNS.");
        Bind(AddButton(quick, "Flush DNS cache", ButtonKind.Accent),
            "Flush DNS cache", () => _diagnostics.FlushDns());
        BindConfirm(AddButton(quick, "Renew DHCP lease", ButtonKind.Ghost),
            "Renew DHCP lease", "Renew DHCP lease",
            "This releases and renews the DHCP lease. The PC will briefly lose network connectivity. Continue?",
            () => _diagnostics.RenewDhcp());
        Bind(AddButton(quick, "Test internet", ButtonKind.Ghost),
            "Test internet connectivity", () => _diagnostics.TestInternet());
        quick.Body.Controls.Add(BodyLabel("DNS host"));
        quick.Body.Controls.Add(dnsHost);
        Bind(AddButton(quick, "Check DNS resolution", ButtonKind.Ghost),
            "Check DNS resolution", () => _diagnostics.CheckDns(dnsHost.Box.Text.Trim()));

        var routing = new Card("Gateway, IP configuration and adapters",
            "Find the default gateway, show the full IP configuration and review adapter state.");
        Bind(AddButton(routing, "Ping default gateway", ButtonKind.Accent),
            "Ping default gateway", () => _diagnostics.PingGateway());
        Bind(AddButton(routing, "Display IP configuration", ButtonKind.Ghost),
            "Display IP configuration", () => _diagnostics.IpConfig());
        Bind(AddButton(routing, "Adapter overview", ButtonKind.Ghost),
            "Adapter overview", () => _diagnostics.AdapterSummary());

        var trace = new Card("Traceroute and multi-host ping",
            "Trace the path to a host, or check latency to the gateway and public endpoints at once.");
        trace.Body.Controls.Add(BodyLabel("Target host"));
        trace.Body.Controls.Add(traceHost);
        Bind(AddButton(trace, "Run traceroute", ButtonKind.Ghost),
            "Traceroute", () => _diagnostics.Traceroute(traceHost.Box.Text.Trim()));
        Bind(AddButton(trace, "Ping multiple hosts", ButtonKind.Ghost),
            "Ping multiple hosts", () => _diagnostics.MultiPing());

        var repairs = new Card("Advanced network repairs",
            "These actions change network state and ask for confirmation first.");
        BindConfirm(AddButton(repairs, "Restart network adapters", ButtonKind.Danger),
            "Restart network adapters", "Restart network adapters",
            "All active network adapters will be restarted. The network will drop for a few seconds. Continue?",
            () => _diagnostics.RestartAdapters());
        BindConfirm(AddButton(repairs, "Flush ARP cache", ButtonKind.Danger),
            "Flush ARP cache", "Flush ARP cache",
            "This clears the ARP cache for all interfaces. Continue?",
            () => _diagnostics.FlushArp());
        BindConfirm(AddButton(repairs, "Release DHCP address", ButtonKind.Danger),
            "Release DHCP address", "Release DHCP address",
            "This releases the current DHCP address and disconnects the adapter until it is renewed. Continue?",
            () => _diagnostics.ReleaseIp());
        BindConfirm(AddButton(repairs, "Reset Winsock", ButtonKind.Danger),
            "Reset Winsock catalog", "Reset Winsock",
            "This resets the Winsock catalog to a clean state. A reboot is required afterwards. Continue?",
            () => _diagnostics.ResetWinsock());
        BindConfirm(AddButton(repairs, "Reset TCP/IP stack", ButtonKind.Danger),
            "Reset TCP/IP stack", "Reset TCP/IP",
            "This resets the TCP/IP stack. A reboot is required afterwards. Continue?",
            () => _diagnostics.ResetTcpIp());
        BindConfirm(AddButton(repairs, "Reset WinHTTP proxy", ButtonKind.Danger),
            "Reset WinHTTP proxy", "Reset WinHTTP proxy",
            "This clears the WinHTTP proxy configuration. Continue?",
            () => _diagnostics.ResetProxy());

        return NewPage(quick, routing, trace, repairs);
    }

    private Panel BuildSystemPage()
    {
        var filter = new RoundedInput(190, "");

        var hardware = new Card("Hardware and storage",
            "Read system specifications and inspect physical disk, volume and SMART health.");
        Bind(AddButton(hardware, "Show PC specifications", ButtonKind.Accent),
            "PC specifications", () => _diagnostics.Specs());
        Bind(AddButton(hardware, "Check disk health", ButtonKind.Ghost),
            "Disk health (SMART)", () => _diagnostics.DiskHealth());

        var services = new Card("Windows services",
            "List services by name, or verify that key Windows services are running.");
        services.Body.Controls.Add(BodyLabel("Filter"));
        services.Body.Controls.Add(filter);
        Bind(AddButton(services, "List services", ButtonKind.Ghost),
            "Windows services", () => _diagnostics.ListServices(filter.Box.Text.Trim()));
        Bind(AddButton(services, "Check critical services", ButtonKind.Ghost),
            "Critical Windows services", () => _diagnostics.CriticalServices());

        var events = new Card("Event log and crash history",
            "Review recent System errors, unexpected shutdowns and available memory dumps.");
        Bind(AddButton(events, "Recent errors and crashes", ButtonKind.Accent),
            "Event log and crash history", () => _diagnostics.SystemEvents());

        var software = new Card("Startup and installed software",
            "Inspect autoruns and installed applications for troubleshooting and cleanup.");
        Bind(AddButton(software, "Startup items and programs", ButtonKind.Ghost),
            "Startup items and installed programs", () => _diagnostics.StartupAndPrograms());

        return NewPage(hardware, services, events, software);
    }

    private Panel BuildSecurityPage()
    {
        var security = new Card("Security posture",
            "Defender status, firewall profiles, network profile, proxy configuration and BitLocker.");
        Bind(AddButton(security, "Run security check", ButtonKind.Accent),
            "Security and firewall status", () => _diagnostics.SecurityStatus());

        return NewPage(security);
    }

    private Panel BuildReportPage()
    {
        var reportCard = new Card("Session report",
            "Every completed check is stored here. Export a shareable report or copy the raw output.");
        _reportCount = new Label
        {
            Text = "0 checks captured",
            Font = Theme.Ui(9f),
            ForeColor = Theme.Muted,
            BackColor = Theme.Card,
            AutoSize = true,
            Margin = new Padding(0, 0, 14, 8)
        };
        reportCard.Body.Controls.Add(_reportCount);

        var exportHtml = AddButton(reportCard, "Export HTML report", ButtonKind.Accent);
        exportHtml.Click += (_, _) => ExportReport(html: true);
        var exportTxt = AddButton(reportCard, "Export text report", ButtonKind.Ghost);
        exportTxt.Click += (_, _) => ExportReport(html: false);
        var copy = AddButton(reportCard, "Copy all output", ButtonKind.Ghost);
        copy.Click += (_, _) => CopyAll();
        var clear = AddButton(reportCard, "Clear results", ButtonKind.Danger);
        clear.Click += (_, _) => ClearResults();

        var suite = new Card("Full diagnostic run",
            "Run a standard health check across network, hardware, services and security.");
        var runAll = AddButton(suite, "Run all checks", ButtonKind.Accent);
        runAll.Click += async (_, _) => await RunAllChecksAsync(runAll);

        return NewPage(reportCard, suite);
    }

    private Panel BuildAboutPage()
    {
        var about = new Card("IT Support Toolbox",
            "Portable Windows diagnostics for IT support engineers.");

        about.Body.Controls.Add(new Label
        {
            Text =
                "Version 1.0.0  \u00B7  single-file portable executable  \u00B7  Windows 10 / 11 (x64)\r\n\r\n" +
                "Network tools\r\n" +
                "  Flush DNS  \u00B7  renew DHCP  \u00B7  ping gateway  \u00B7  test internet  \u00B7  DNS resolution\r\n" +
                "  IP configuration  \u00B7  adapter overview  \u00B7  restart adapters  \u00B7  traceroute\r\n" +
                "  multi-host ping  \u00B7  flush ARP  \u00B7  release IP  \u00B7  reset Winsock / TCP-IP / WinHTTP proxy\r\n\r\n" +
                "System tools\r\n" +
                "  PC specifications  \u00B7  disk and SMART health  \u00B7  Windows services\r\n" +
                "  event log and crash history  \u00B7  startup items  \u00B7  installed programs\r\n\r\n" +
                "Security tools\r\n" +
                "  Defender status  \u00B7  firewall profiles  \u00B7  network profile  \u00B7  proxy  \u00B7  BitLocker\r\n\r\n" +
                "The app requests administrator rights at launch so repair actions work. " +
                "If elevation is declined, it still runs in read-only mode and shows the results it can collect.\r\n\r\n" +
                "Reports are written only when you choose to export them. Nothing is uploaded.",
            Font = Theme.Ui(9.25f),
            ForeColor = Color.FromArgb(198, 206, 220),
            BackColor = Theme.Card,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Margin = Padding.Empty
        });

        return NewPage(about);
    }

    private static Label BodyLabel(string text) => new()
    {
        Text = text,
        Font = Theme.Ui(9.25f),
        ForeColor = Theme.Muted,
        BackColor = Theme.Card,
        AutoSize = true,
        Margin = new Padding(0, 11, 8, 8)
    };

    private static AccentButton AddButton(Card card, string text, ButtonKind kind = ButtonKind.Ghost)
    {
        var button = new AccentButton(text, kind);
        card.Body.Controls.Add(button);
        return button;
    }

    private void Bind(AccentButton button, string name, Func<Task<ToolResult>> action)
    {
        button.Click += async (_, _) => await ExecuteAsync(name, action, button);
    }

    private void BindConfirm(AccentButton button, string name, string title, string message,
        Func<Task<ToolResult>> action)
    {
        button.Click += async (_, _) =>
        {
            if (ModalDialog.Confirm(this, title, message, "Continue", danger: true) != DialogResult.OK)
                return;
            await ExecuteAsync(name, action, button);
        };
    }

    private async Task ExecuteAsync(string name, Func<Task<ToolResult>> action, Control? trigger)
    {
        if (trigger is not null)
            trigger.Enabled = false;

        await _gate.WaitAsync();
        try
        {
            SetStatus("Running: " + name);
            var result = await action();
            AddResult(result);
        }
        catch (Exception ex)
        {
            AddResult(new ToolResult(name, "Unexpected error: " + ex.Message, false, DateTime.Now, TimeSpan.Zero));
        }
        finally
        {
            SetStatus(null);
            if (trigger is not null)
                trigger.Enabled = true;
            _gate.Release();
        }
    }

    private void AddResult(ToolResult result)
    {
        _report.Add(result);
        _output.Append(result);
    }

    private void SetStatus(string? status)
    {
        if (_statusLabel.IsDisposed)
            return;

        _statusLabel.Text = status ?? "Ready";
        _statusLabel.ForeColor = status is null ? Theme.Muted : Theme.Accent;
    }

    private void ShowPage(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
            return;

        _currentPage = key;
        foreach (var entry in _pages)
            entry.Value.Visible = entry.Key == key;
        foreach (var nav in _navButtons)
            nav.Active = (string?)nav.Tag == key;

        if (PageInfo.TryGetValue(key, out var info))
        {
            _pageTitle.Text = info.Title;
            _pageSubtitle.Text = info.Subtitle;
        }
    }

    private void UpdateReportCount()
    {
        if (_reportCount is null || _reportCount.IsDisposed)
            return;

        var items = _report.Items;
        int ok = items.Count(i => i.Success == true);
        int fail = items.Count(i => i.Success == false);
        string checks = items.Count == 1 ? "check" : "checks";
        _reportCount.Text = $"{items.Count} {checks} captured  \u00B7  {ok} passed  \u00B7  {fail} failed";
    }

    private void ExportReport(bool html)
    {
        var items = _report.Items;
        if (items.Count == 0)
        {
            ModalDialog.Info(this, "Nothing to export",
                "Run a few checks first. Results are captured automatically, then exported together.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = html ? "Export HTML report" : "Export text report",
            Filter = html ? "HTML report (*.html)|*.html" : "Text report (*.txt)|*.txt",
            FileName = ReportWriter.SuggestedFileName(html ? "html" : "txt"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            string content = html
                ? ReportWriter.Html(items, Environment.MachineName, _isAdmin)
                : ReportWriter.PlainText(items, Environment.MachineName, _isAdmin);
            File.WriteAllText(dialog.FileName, content, new System.Text.UTF8Encoding(false));
            _output.AppendNote("Report saved", dialog.FileName, ok: true);
        }
        catch (Exception ex)
        {
            ModalDialog.Info(this, "Export failed", ex.Message);
        }
    }

    private void CopyAll()
    {
        if (_report.Items.Count == 0)
        {
            ModalDialog.Info(this, "Nothing to copy", "Run a check first, then copy the output.");
            return;
        }

        try
        {
            Clipboard.SetText(_output.AllText);
            _output.AppendNote("Copied to clipboard", "All console output was copied.", ok: true);
        }
        catch (Exception ex)
        {
            ModalDialog.Info(this, "Copy failed", ex.Message);
        }
    }

    private void ClearResults()
    {
        if (_report.Items.Count == 0)
            return;

        if (ModalDialog.Confirm(this, "Clear results",
                "Remove all captured checks and clear the output console?", "Clear", danger: true) != DialogResult.OK)
            return;

        _report.Clear();
        _output.ClearOutput();
    }

    private async Task RunAllChecksAsync(Control? trigger)
    {
        if (trigger is not null)
            trigger.Enabled = false;

        try
        {
            AddResult(new ToolResult("Standard diagnostic suite",
                "Running a quick health check across network, hardware, services and security.",
                null, DateTime.Now, TimeSpan.Zero));

            await ExecuteAsync("Adapter overview", () => _diagnostics.AdapterSummary(), null);
            await ExecuteAsync("Ping default gateway", () => _diagnostics.PingGateway(), null);
            await ExecuteAsync("Test internet connectivity", () => _diagnostics.TestInternet(), null);
            await ExecuteAsync("Check DNS resolution", () => _diagnostics.CheckDns("www.google.com"), null);
            await ExecuteAsync("PC specifications", () => _diagnostics.Specs(), null);
            await ExecuteAsync("Disk health (SMART)", () => _diagnostics.DiskHealth(), null);
            await ExecuteAsync("Critical Windows services", () => _diagnostics.CriticalServices(), null);
            await ExecuteAsync("Security and firewall status", () => _diagnostics.SecurityStatus(), null);

            AddResult(new ToolResult("Diagnostic suite complete",
                "Review the results above or export the report from the Report page.",
                true, DateTime.Now, TimeSpan.Zero));
            ShowPage("report");
        }
        finally
        {
            if (trigger is not null)
                trigger.Enabled = true;
        }
    }

}
