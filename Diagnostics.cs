using System.Net;
using System.Text.RegularExpressions;

namespace ItSupportToolbox;

internal sealed class Diagnostics
{
    private readonly Shell _shell;

    public Diagnostics(Shell shell) => _shell = shell;

    private static bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253)
            return false;
        return Regex.IsMatch(host, @"^[A-Za-z0-9]([A-Za-z0-9\-\.]*[A-Za-z0-9])?$") ||
               IPAddress.TryParse(host, out _);
    }

    public async Task<string?> GetGatewayAsync()
    {
        var (_, output) = await _shell.ExecPs(
            "$up = @(Get-NetAdapter -ErrorAction SilentlyContinue | " +
            "Where-Object { $_.Status -eq 'Up' } | Select-Object -ExpandProperty InterfaceIndex); " +
            "Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | " +
            "Where-Object { $up -contains $_.InterfaceIndex -and $_.NextHop -ne '0.0.0.0' } | " +
            "Sort-Object RouteMetric, InterfaceMetric | " +
            "Select-Object -First 1 -ExpandProperty NextHop",
            20_000);

        string value = output.Trim().Split('\n')[0].Trim();
        return IPAddress.TryParse(value, out _) ? value : null;
    }

    public async Task<ToolResult> FlushDns()
        => await _shell.Native("Flush DNS cache", "ipconfig.exe", "/flushdns");

    public async Task<ToolResult> RenewDhcp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var release = await _shell.ExecNative("ipconfig.exe", new[] { "/release" }, 60_000);
        var renew = await _shell.ExecNative("ipconfig.exe", new[] { "/renew" }, 180_000);
        sw.Stop();

        string output =
            "ipconfig /release" + Environment.NewLine +
            release.Output + Environment.NewLine + Environment.NewLine +
            "ipconfig /renew" + Environment.NewLine +
            renew.Output;

        bool ok = renew.Code == 0;
        return new ToolResult("Renew DHCP lease", output.Trim(), ok, DateTime.Now, sw.Elapsed);
    }

    public async Task<ToolResult> PingGateway()
    {
        string? gateway = await GetGatewayAsync();
        if (gateway is null)
        {
            return new ToolResult("Ping default gateway",
                "No IPv4 default gateway was found. The adapter may be disconnected or configured with a static address.",
                false, DateTime.Now, TimeSpan.Zero);
        }

        var result = await _shell.Native($"Ping default gateway ({gateway})", 45_000,
            "ping.exe", "-n", "4", "-w", "1500", gateway);

        return result with { Output = $"Gateway: {gateway}{Environment.NewLine}{Environment.NewLine}{result.Output}" };
    }

    public async Task<ToolResult> TestInternet()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var ping1 = _shell.ExecNative("ping.exe", new[] { "-n", "3", "-w", "1500", "1.1.1.1" }, 30_000);
        var ping2 = _shell.ExecNative("ping.exe", new[] { "-n", "3", "-w", "1500", "8.8.8.8" }, 30_000);
        var http = _shell.ExecPs(
            "try { $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://www.msftconnecttest.com/connecttest.txt' -TimeoutSec 8; " +
            "'HTTP test: ' + $r.Content.Trim() } " +
            "catch { 'HTTP test failed: ' + $_.Exception.Message }",
            20_000);

        await Task.WhenAll(ping1, ping2, http);
        var r1 = await ping1;
        var r2 = await ping2;
        var r3 = await http;
        sw.Stop();

        bool httpOk = r3.Output.Contains("HTTP test:", StringComparison.Ordinal) &&
                      !r3.Output.Contains("failed", StringComparison.OrdinalIgnoreCase);
        bool ok = r1.Code == 0 || r2.Code == 0 || httpOk;

        string output =
            "=== Ping 1.1.1.1 ===" + Environment.NewLine + r1.Output + Environment.NewLine + Environment.NewLine +
            "=== Ping 8.8.8.8 ===" + Environment.NewLine + r2.Output + Environment.NewLine + Environment.NewLine +
            "=== HTTP connectivity ===" + Environment.NewLine + r3.Output;

        return new ToolResult("Test internet connectivity", output.Trim(), ok, DateTime.Now, sw.Elapsed);
    }

    public async Task<ToolResult> CheckDns(string host)
    {
        if (!IsValidHost(host))
        {
            return new ToolResult("Check DNS resolution",
                $"\"{host}\" is not a valid hostname or IP address.", false, DateTime.Now, TimeSpan.Zero);
        }

        string safe = host.Replace("'", "''");
        string script = $$"""
$h = '{{safe}}'
'DNS servers configured:'
Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.ServerAddresses } |
    ForEach-Object { '  ' + $_.InterfaceAlias + ': ' + ($_.ServerAddresses -join ', ') }
''
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $r = Resolve-DnsName -Name $h -Type A -DnsOnly -ErrorAction Stop
    $sw.Stop()
    'Resolved ' + $h + ' in ' + $sw.ElapsedMilliseconds + ' ms:'
    $r | Where-Object { $_.IPAddress } |
        Select-Object Name, Type, IPAddress, TTL |
        Format-Table -AutoSize | Out-String -Width 4096
} catch {
    $sw.Stop()
    'RESOLUTION FAILED after ' + $sw.ElapsedMilliseconds + ' ms: ' + $_.Exception.Message
}
""";

        var result = await _shell.Ps($"Check DNS resolution ({host})", script, 45_000);
        return result with { Success = !result.Output.Contains("RESOLUTION FAILED", StringComparison.Ordinal) };
    }

    public async Task<ToolResult> IpConfig()
        => await _shell.Native("Display IP configuration", 45_000, "ipconfig.exe", "/all");

    public async Task<ToolResult> AdapterSummary()
    {
        const string script = """
'NETWORK ADAPTERS'
Get-NetAdapter -ErrorAction SilentlyContinue |
    Sort-Object Status, Name |
    Select-Object Name, InterfaceDescription, Status, LinkSpeed, MacAddress |
    Format-Table -AutoSize | Out-String -Width 4096

'IP ADDRESSES (IPv4)'
Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -ne '127.0.0.1' } |
    Select-Object InterfaceAlias, IPAddress, PrefixLength, PrefixOrigin, SuffixOrigin |
    Format-Table -AutoSize | Out-String -Width 4096

'DEFAULT GATEWAY'
Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
    Sort-Object RouteMetric |
    Select-Object -First 3 InterfaceAlias, NextHop, RouteMetric |
    Format-Table -AutoSize | Out-String -Width 4096
""";
        return await _shell.Ps("Adapter overview", script, 45_000);
    }

    public async Task<ToolResult> RestartAdapters()
    {
        const string script = """
$adapters = @(Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' -and $_.HardwareInterface })
if ($adapters.Count -eq 0) {
    'No active hardware adapters were found.'
    return
}

foreach ($a in $adapters) {
    'Restarting ' + $a.Name + ' (' + $a.InterfaceDescription + ')...'
    try {
        Restart-NetAdapter -Name $a.Name -Confirm:$false -ErrorAction Stop
        Start-Sleep -Seconds 3
        $s = Get-NetAdapter -Name $a.Name -ErrorAction SilentlyContinue
        '  -> ' + $a.Name + ': ' + $(if ($s) { $s.Status } else { 'unknown' })
    } catch {
        '  -> FAILED: ' + $_.Exception.Message
    }
}
""";
        var result = await _shell.Ps("Restart network adapters", script, 180_000);
        return result with { Success = !result.Output.Contains("FAILED", StringComparison.Ordinal) };
    }

    public async Task<ToolResult> Traceroute(string host)
    {
        if (!IsValidHost(host))
        {
            return new ToolResult("Traceroute",
                $"\"{host}\" is not a valid hostname or IP address.", false, DateTime.Now, TimeSpan.Zero);
        }

        return await _shell.Native($"Traceroute ({host})", 180_000,
            "tracert.exe", "-d", "-h", "20", "-w", "1000", host);
    }

    public async Task<ToolResult> MultiPing()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? gateway = await GetGatewayAsync();

        var targets = new List<string>();
        if (gateway is not null) targets.Add(gateway);
        targets.Add("1.1.1.1");
        targets.Add("8.8.8.8");
        targets.Add("www.google.com");

        var tasks = targets.Select(t => _shell.ExecNative("ping.exe",
            new[] { "-n", "3", "-w", "1200", t }, 30_000)).ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        var lines = new List<string>();
        bool anyOk = false;
        for (int i = 0; i < targets.Count; i++)
        {
            var r = await tasks[i];
            if (r.Code == 0) anyOk = true;
            string label = targets[i] == gateway ? $"Default gateway ({targets[i]})" : targets[i];
            lines.Add($"=== {label} ===");
            lines.Add(r.Output);
            lines.Add("");
        }

        return new ToolResult("Ping multiple hosts", string.Join(Environment.NewLine, lines).Trim(),
            anyOk, DateTime.Now, sw.Elapsed);
    }

    public async Task<ToolResult> FlushArp()
        => await _shell.Native("Flush ARP cache", "netsh.exe", "interface", "ip", "delete", "arpcache");

    public async Task<ToolResult> ReleaseIp()
    {
        var result = await _shell.Native("Release DHCP address", 60_000, "ipconfig.exe", "/release");
        return result with
        {
            Output = result.Output + Environment.NewLine + Environment.NewLine +
                     "The adapter is now disconnected. Run \"Renew DHCP lease\" to obtain an address again."
        };
    }

    public async Task<ToolResult> ResetWinsock()
    {
        var result = await _shell.Native("Reset Winsock catalog", "netsh.exe", "winsock", "reset");
        return result with
        {
            Output = result.Output + Environment.NewLine + Environment.NewLine +
                     "A reboot is required to complete the Winsock reset."
        };
    }

    public async Task<ToolResult> ResetTcpIp()
    {
        var result = await _shell.Native("Reset TCP/IP stack", "netsh.exe", "int", "ip", "reset");
        return result with
        {
            Output = result.Output + Environment.NewLine + Environment.NewLine +
                     "A reboot is required to complete the TCP/IP reset."
        };
    }

    public async Task<ToolResult> ResetProxy()
        => await _shell.Native("Reset WinHTTP proxy", "netsh.exe", "winhttp", "reset", "proxy");

    public async Task<ToolResult> Specs()
    {
        const string script = """
function L($k, $v) { '{0,-22} {1}' -f ($k + ':'), $v }

$os    = Get-CimInstance Win32_OperatingSystem
$cs    = Get-CimInstance Win32_ComputerSystem
$cpu   = Get-CimInstance Win32_Processor | Select-Object -First 1
$bios  = Get-CimInstance Win32_BIOS
$board = Get-CimInstance Win32_BaseBoard
$gpu   = @(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name }) -join '; '
$ramGB = [math]::Round(([double](Get-CimInstance Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum).Sum) / 1GB, 1)
$biosDate = if ($bios.ReleaseDate) { $bios.ReleaseDate.ToString('yyyy-MM-dd') } else { 'Unknown' }
$up = (Get-Date) - $os.LastBootUpTime
$res = @(Get-CimInstance Win32_VideoController | Where-Object { $_.CurrentHorizontalResolution } |
    ForEach-Object { "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution)" }) -join ', '

L 'Computer name' $env:COMPUTERNAME
L 'Manufacturer' ($cs.Manufacturer + ' ' + $cs.Model)
L 'Operating system' ($os.Caption + ' ' + $os.Version + ' (Build ' + $os.BuildNumber + ')')
L 'Architecture' $os.OSArchitecture
L 'CPU' $cpu.Name
L 'Cores / threads' ("$($cpu.NumberOfCores) / $($cpu.NumberOfLogicalProcessors)")
L 'Memory' ("{0:N1} GB" -f $ramGB)
L 'Graphics' $gpu
L 'Resolution' $(if ($res) { $res } else { 'Unknown' })
L 'BIOS' ($bios.SMBIOSBIOSVersion + ' (' + $biosDate + ')')
L 'Motherboard' ($board.Manufacturer + ' ' + $board.Product)
L 'Serial number' $bios.SerialNumber
L 'Windows install' $os.InstallDate
L 'Last boot' $os.LastBootUpTime
L 'Uptime' ("{0}d {1}h {2}m" -f $up.Days, $up.Hours, $up.Minutes)
L 'Domain / workgroup' $(if ($cs.PartOfDomain) { $cs.Domain } else { 'Workgroup: ' + $cs.Workgroup })
""";
        return await _shell.Ps("PC specifications", script, 60_000);
    }

    public async Task<ToolResult> DiskHealth()
    {
        const string script = """
'PHYSICAL DISKS'
Get-PhysicalDisk -ErrorAction SilentlyContinue |
    Select-Object DeviceId, FriendlyName, MediaType, HealthStatus, OperationalStatus,
        @{n = 'SizeGB'; e = { [math]::Round($_.Size / 1GB, 0) } } |
    Format-Table -AutoSize | Out-String -Width 4096

'VOLUMES'
Get-Volume -ErrorAction SilentlyContinue |
    Where-Object { $_.DriveLetter } |
    Select-Object DriveLetter, FileSystemLabel, FileSystem, HealthStatus,
        @{n = 'SizeGB'; e = { [math]::Round($_.Size / 1GB, 1) } },
        @{n = 'FreeGB'; e = { [math]::Round($_.SizeRemaining / 1GB, 1) } },
        @{n = 'Free%'; e = { if ($_.Size -gt 0) { [math]::Round(($_.SizeRemaining / $_.Size) * 100, 0) } else { 0 } } } |
    Format-Table -AutoSize | Out-String -Width 4096

'RELIABILITY / SMART COUNTERS'
try {
    $rows = @(Get-PhysicalDisk -ErrorAction SilentlyContinue | ForEach-Object {
        $disk = $_
        $c = Get-StorageReliabilityCounter -PhysicalDisk $disk -ErrorAction SilentlyContinue
        if ($c) {
            [pscustomobject]@{
                Disk           = $disk.FriendlyName
                Temperature    = $c.Temperature
                Wear           = $c.Wear
                ReadErrors     = $c.ReadErrorsTotal
                WriteErrors    = $c.WriteErrorsTotal
                PowerOnHours   = $c.PowerOnHours
            }
        }
    })
    if ($rows.Count -gt 0) {
        $rows | Format-Table -AutoSize | Out-String -Width 4096
    } else {
        'No SMART reliability counters are available on this system.'
    }
} catch {
    'SMART counters unavailable: ' + $_.Exception.Message
}
""";
        var result = await _shell.Ps("Disk health (SMART)", script, 60_000);
        bool bad = result.Output.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase) ||
                   result.Output.Contains("Warning", StringComparison.OrdinalIgnoreCase);
        return result with { Success = result.Success == true && !bad };
    }

    public async Task<ToolResult> CriticalServices()
    {
        const string script = """
$names = 'Dhcp','Dnscache','LanmanWorkstation','LanmanServer','W32Time','Winmgmt','EventLog',
         'BFE','MpsSvc','WinDefend','Spooler','AudioSrv','netprofm','NlaSvc','DcomLaunch',
         'RpcSs','Themes','WSearch','TermService','SessionEnv'

$svc = @($names | ForEach-Object {
    $s = Get-CimInstance Win32_Service -Filter "Name='$_'" -ErrorAction SilentlyContinue
    if ($s) {
        [pscustomobject]@{ Name = $s.Name; DisplayName = $s.DisplayName; Status = $s.State; StartMode = $s.StartMode }
    }
} | Sort-Object Status, Name)

if ($svc.Count -eq 0) {
    'No monitored services were found.'
    return
}

$svc | Select-Object Status, StartMode, Name, DisplayName | Format-Table -AutoSize | Out-String -Width 4096
$stopped = @($svc | Where-Object { $_.Status -ne 'Running' })
$autoStopped = @($stopped | Where-Object { $_.StartMode -eq 'Auto' -or $_.StartMode -eq 'Automatic' })
''
'Monitored: ' + $svc.Count + '   Stopped: ' + $stopped.Count + '   Automatic stopped: ' + $autoStopped.Count
if ($autoStopped.Count -gt 0) {
    'ACTION: Automatic services stopped: ' + (($autoStopped | ForEach-Object Name) -join ', ')
} else {
    'ACTION: All monitored automatic services are running.'
}
""";
        var result = await _shell.Ps("Critical Windows services", script, 45_000);
        bool? success = null;
        if (result.Output.Contains("Automatic stopped: 0", StringComparison.Ordinal))
            success = true;
        else if (result.Output.Contains("Automatic stopped:", StringComparison.Ordinal))
            success = false;
        return result with { Success = success };
    }

    public async Task<ToolResult> ListServices(string filter)
    {
        string safe = Regex.Replace(filter ?? string.Empty, @"[^A-Za-z0-9 _\-\.]", string.Empty);
        string script = $$"""
$f = '{{safe}}'
if ($f) {
    $svc = @(Get-Service -Name ('*' + $f + '*') -ErrorAction SilentlyContinue)
} else {
    $svc = @(Get-Service -ErrorAction SilentlyContinue)
}

if ($svc.Count -eq 0) {
    'No services matched the filter.'
    return
}

$svc | Sort-Object Status, DisplayName |
    Select-Object Status, StartType, Name, DisplayName |
    Format-Table -AutoSize | Out-String -Width 4096
'Total: ' + $svc.Count
""";
        string title = string.IsNullOrWhiteSpace(safe) ? "All Windows services" : $"Windows services (*{safe}*)";
        return await _shell.Ps(title, script, 60_000);
    }

    public async Task<ToolResult> SystemEvents()
    {
        const string script = """
'RECENT SYSTEM ERRORS / WARNINGS (last 7 days)'
$events = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; Level = 1, 2, 3; StartTime = (Get-Date).AddDays(-7) } -MaxEvents 30 -ErrorAction SilentlyContinue)
if ($events.Count -gt 0) {
    $events | Select-Object TimeCreated, Id, LevelDisplayName, ProviderName,
        @{n = 'Message'; e = { ($_.Message -split "`r?`n")[0] } } |
        Format-Table -AutoSize -Wrap | Out-String -Width 4096
} else {
    'No error or warning events were found in the last 7 days.'
}

'UNEXPECTED SHUTDOWNS / BUGCHECKS (last 30 days)'
$crashes = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; Id = 41, 1001, 6008; StartTime = (Get-Date).AddDays(-30) } -MaxEvents 20 -ErrorAction SilentlyContinue)
if ($crashes.Count -gt 0) {
    $crashes | Select-Object TimeCreated, Id, ProviderName,
        @{n = 'Message'; e = { ($_.Message -split "`r?`n")[0] } } |
        Format-Table -AutoSize -Wrap | Out-String -Width 4096
} else {
    'No unexpected shutdowns or bugchecks recorded.'
}

'MEMORY DUMPS'
$dumps = @(Get-ChildItem 'C:\Windows\Minidump' -Filter *.dmp -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
if ($dumps.Count -gt 0) {
    $dumps | Select-Object Name, LastWriteTime, @{n = 'SizeMB'; e = { [math]::Round($_.Length / 1MB, 1) } } |
        Format-Table -AutoSize | Out-String -Width 4096
} else {
    'No minidump files found.'
}
$full = Get-Item 'C:\Windows\MEMORY.DMP' -ErrorAction SilentlyContinue
if ($full) { 'Kernel dump: ' + $full.LastWriteTime + ' (' + [math]::Round($full.Length / 1MB, 0) + ' MB)' }
""";
        return await _shell.Ps("Event log and crash history", script, 90_000);
    }

    public async Task<ToolResult> StartupAndPrograms()
    {
        const string script = """
'STARTUP ITEMS'
$startup = @(Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue | Sort-Object Name)
if ($startup.Count -gt 0) {
    $startup | Select-Object Name, Location, User, Command |
        Format-Table -AutoSize -Wrap | Out-String -Width 4096
} else {
    'No startup items reported.'
}

'INSTALLED PROGRAMS'
$keys = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
)
$apps = @(Get-ItemProperty $keys -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName } |
    Select-Object DisplayName, DisplayVersion, Publisher, InstallDate |
    Sort-Object DisplayName -Unique)
$apps | Format-Table -AutoSize | Out-String -Width 4096
'Total programs: ' + $apps.Count
""";
        return await _shell.Ps("Startup items and installed programs", script, 90_000);
    }

    public async Task<ToolResult> SecurityStatus()
    {
        const string script = """
'MICROSOFT DEFENDER'
try {
    Get-MpComputerStatus -ErrorAction Stop |
        Select-Object AMServiceEnabled, AntivirusEnabled, RealTimeProtectionEnabled,
            AntispywareEnabled, BehaviorMonitorEnabled, AntivirusSignatureVersion,
            AntivirusSignatureLastUpdated, QuickScanAge, FullScanAge, ComputerState |
        Format-List | Out-String -Width 4096
} catch {
    'Defender status unavailable: ' + $_.Exception.Message
}

'FIREWALL PROFILES'
Get-NetFirewallProfile -ErrorAction SilentlyContinue |
    Select-Object Name, Enabled, DefaultInboundAction, DefaultOutboundAction |
    Format-Table -AutoSize | Out-String -Width 4096

'NETWORK PROFILES'
Get-NetConnectionProfile -ErrorAction SilentlyContinue |
    Select-Object Name, InterfaceAlias, NetworkCategory, IPv4Connectivity, IPv6Connectivity |
    Format-Table -AutoSize | Out-String -Width 4096

'WINHTTP PROXY'
(netsh winhttp show proxy) | Out-String

'USER PROXY SETTINGS'
$p = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction SilentlyContinue
if ($p) { 'ProxyEnable = ' + $p.ProxyEnable + '   ProxyServer = ' + $p.ProxyServer } else { 'Not configured.' }

'BITLOCKER'
try {
    Get-BitLockerVolume -ErrorAction Stop |
        Select-Object MountPoint, VolumeStatus, ProtectionStatus, EncryptionPercentage |
        Format-Table -AutoSize | Out-String -Width 4096
} catch {
    'BitLocker status unavailable: ' + $_.Exception.Message
}
""";
        return await _shell.Ps("Security and firewall status", script, 60_000);
    }
}
