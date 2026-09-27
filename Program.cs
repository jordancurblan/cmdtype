using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;
App.Run();

// ─────────────────────────────────────────────────────────────
//  cmdtype — Monkeytype-style typing test for cmd + PowerShell
// ─────────────────────────────────────────────────────────────

record Cmd(bool Ps, string Text);

enum After { Again, Menu, Quit }

static class App
{
    const string Reset = "\x1b[0m";
    const string Bold = "\x1b[1m";
    const string Under = "\x1b[4m";

    static readonly Random Rng = new();
    static readonly string User = Environment.UserName;
    static Settings S = Settings.Load();

    public static void Run()
    {
        EnableVT();
        while (true)
        {
            var pick = Menu();
            if (pick is null) return;

            After a;
            do { a = Test(pick.Value.timed, pick.Value.amount); } while (a == After.Again);
            if (a == After.Quit) return;
        }
    }

    // ── main menu ────────────────────────────────────────────
    static (bool timed, int amount)? Menu()
    {
        while (true)
        {
            Console.Title = "cmdtype";
            Console.Clear();
            Banner();
            Console.Write(C(S.Prompt) +
                "  [1] timed 30s\n" +
                "  [2] timed 60s\n" +
                "  [3] 10 commands\n" +
                "  [4] 25 commands\n\n" +
                "  [5] colors\n" +
                "  [esc] quit\n" + Reset);

            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape) return null;
            switch (k.KeyChar)
            {
                case '1': return (true, 30);
                case '2': return (true, 60);
                case '3': return (false, 10);
                case '4': return (false, 25);
                case '5': Colors(); break;
            }
        }
    }

    static void Banner() => Console.Write(C(S.Prompt) +
        "cmdtype [Version 1.0.0]\n(c) Jordan Jones. All rights reserved.\n\n" + Reset);

    // ── the typing test ──────────────────────────────────────
    static After Test(bool timed, int amount)
    {
        Console.Clear();
        Banner();

        var sw = new Stopwatch();
        int keys = 0, good = 0, chars = 0, done = 0, lastSec = -1;
        bool timeUp = false;
        Cmd? prev = null;
        Console.Title = timed ? $"cmdtype - {amount}s" : $"cmdtype - 0/{amount}";

        while (timed ? !timeUp : done < amount)
        {
            var cmd = Next(prev);
            prev = cmd;
            string t = cmd.Text;
            string prompt = cmd.Ps ? $"PS C:\\Users\\{User}> " : $"C:\\Users\\{User}>";
            var typed = new StringBuilder();

            // prompt + ghost text, then jump the cursor back to the start of the ghost
            Console.Write(C(S.Prompt) + prompt + C(S.Ghost) + t + Reset + $"\x1b[{t.Length}D");

            while (true)
            {
                if (timed && sw.IsRunning)
                {
                    int left = amount - (int)sw.Elapsed.TotalSeconds;
                    if (left != lastSec) { lastSec = left; Console.Title = $"cmdtype - {Math.Max(left, 0)}s"; }
                    if (sw.Elapsed.TotalSeconds >= amount)
                    {
                        timeUp = true;
                        for (int i = 0; i < typed.Length; i++) if (typed[i] == t[i]) chars++;
                        break;
                    }
                }

                if (!Console.KeyAvailable) { Thread.Sleep(5); continue; }
                var k = Console.ReadKey(true);

                if (k.Key == ConsoleKey.Escape) { Console.WriteLine(); return After.Menu; }

                if (k.Key == ConsoleKey.Enter)
                {
                    // like a real shell: Enter submits, but only once it's typed right
                    if (typed.ToString() == t) { chars += t.Length + 1; done++; break; }
                    continue;
                }

                if (k.Key == ConsoleKey.Backspace)
                {
                    if (typed.Length > 0)
                    {
                        typed.Length--;
                        Console.Write("\b" + C(S.Ghost) + t[typed.Length] + Reset + "\b");
                    }
                    continue;
                }

                char c = k.KeyChar;
                if (char.IsControl(c) || typed.Length >= t.Length) continue;
                if (!sw.IsRunning) sw.Start();

                char want = t[typed.Length];
                bool ok = c == want;
                keys++;
                if (ok) good++;
                typed.Append(c);

                Console.Write(ok
                    ? C(S.Correct) + want + Reset
                    : C(S.Wrong) + Bold + Under + want + Reset);
            }

            Console.Write("\n\n");
            if (!timed) Console.Title = $"cmdtype - {done}/{amount}";
        }

        // ── results ──
        sw.Stop();
        double min = Math.Max(sw.Elapsed.TotalMinutes, 1e-9);
        int wpm = (int)Math.Round(chars / 5.0 / min);
        int acc = keys == 0 ? 0 : (int)Math.Round(100.0 * good / keys);

        Console.Title = "cmdtype - results";
        Console.Write(C(S.Prompt) + "-- results --\n\n");
        Console.Write($"  wpm   {C(S.Wrong)}{Bold}{wpm}{Reset}{C(S.Prompt)}\n");
        Console.Write($"  acc   {acc}%\n");
        Console.Write($"  cmds  {done}\n");
        Console.Write($"  time  {sw.Elapsed.TotalSeconds:0.0}s\n\n");
        Console.Write("  [enter] again   [m] menu   [esc] quit\n" + Reset);

        // toss any keys you were still mashing when time ran out
        Thread.Sleep(400);
        while (Console.KeyAvailable) Console.ReadKey(true);

        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Enter) return After.Again;
            if (k.Key == ConsoleKey.Escape) return After.Quit;
            if (char.ToLower(k.KeyChar) == 'm') return After.Menu;
        }
    }

    static Cmd Next(Cmd? prev)
    {
        Cmd c;
        do c = Commands.All[Rng.Next(Commands.All.Length)]; while (c == prev);
        return c;
    }

    // ── colors menu ──────────────────────────────────────────
    static void Colors()
    {
        while (true)
        {
            Console.Clear();
            Console.Title = "cmdtype - colors";
            Console.Write(C(S.Prompt) + "colors (hex, like FF00FF)\n\n");
            Row('1', "ghost text", S.Ghost);
            Row('2', "correct", S.Correct);
            Row('3', "wrong", S.Wrong);
            Row('4', "prompt/menu", S.Prompt);
            Console.Write(C(S.Prompt) + "\n  [r] reset defaults\n  [esc] back\n" + Reset);

            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape) return;
            if (char.ToLower(k.KeyChar) == 'r') { S = new Settings(); S.Save(); continue; }
            if (k.KeyChar is < '1' or > '4') continue;

            Console.Write(C(S.Prompt) + "\n  new hex: " + Reset);
            string? hex = Console.ReadLine()?.Trim().TrimStart('#').ToUpper();
            if (hex is null || !Regex.IsMatch(hex, "^[0-9A-F]{6}$")) continue;

            switch (k.KeyChar)
            {
                case '1': S.Ghost = hex; break;
                case '2': S.Correct = hex; break;
                case '3': S.Wrong = hex; break;
                case '4': S.Prompt = hex; break;
            }
            S.Save();
        }
    }

    static void Row(char key, string name, string hex) =>
        Console.Write($"{C(S.Prompt)}  [{key}] {name,-12} {C(hex)}████ ipconfig /all  #{hex}{Reset}\n");

    // hex like "FFFFFF" -> 24-bit ANSI foreground color
    static string C(string hex)
    {
        int r = Convert.ToInt32(hex[0..2], 16);
        int g = Convert.ToInt32(hex[2..4], 16);
        int b = Convert.ToInt32(hex[4..6], 16);
        return $"\x1b[38;2;{r};{g};{b}m";
    }

    // lets the classic console window understand the color codes
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr h, out uint mode);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr h, uint mode);

    static void EnableVT()
    {
        if (!OperatingSystem.IsWindows()) return;
        var h = GetStdHandle(-11);
        if (GetConsoleMode(h, out uint m)) SetConsoleMode(h, m | 0x0004);
    }
}

// ── saved colors (%APPDATA%\cmdtype\settings.txt) ────────────
class Settings
{
    public string Ghost = "5A5A5A";   // dim gray
    public string Correct = "C8C8C8"; // soft white
    public string Wrong = "FFFFFF";   // neon white (bold + underline)
    public string Prompt = "CCCCCC";  // classic cmd gray

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cmdtype");
    static readonly string FilePath = Path.Combine(Dir, "settings.txt");

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var p = line.Split('=', 2);
                if (p.Length != 2) continue;
                string v = p[1].Trim().ToUpper();
                if (!Regex.IsMatch(v, "^[0-9A-F]{6}$")) continue;
                switch (p[0].Trim())
                {
                    case "ghost": s.Ghost = v; break;
                    case "correct": s.Correct = v; break;
                    case "wrong": s.Wrong = v; break;
                    case "prompt": s.Prompt = v; break;
                }
            }
        }
        catch { }
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(FilePath, new[]
            {
                $"ghost={Ghost}", $"correct={Correct}", $"wrong={Wrong}", $"prompt={Prompt}"
            });
        }
        catch { }
    }
}

// ── the command pool (mixed cmd + PowerShell) ────────────────
static class Commands
{
    static Cmd D(string s) => new(false, s); // cmd
    static Cmd P(string s) => new(true, s);  // PowerShell

    public static readonly Cmd[] All =
    [
        // cmd
        D("ipconfig"),
        D("ipconfig /all"),
        D("ipconfig /release"),
        D("ipconfig /renew"),
        D("ipconfig /flushdns"),
        D("ipconfig /displaydns"),
        D("ping 8.8.8.8"),
        D("ping -t 1.1.1.1"),
        D("ping -n 10 google.com"),
        D("tracert 8.8.8.8"),
        D("tracert -d 1.1.1.1"),
        D("pathping google.com"),
        D("nslookup google.com"),
        D("nslookup -type=mx gmail.com"),
        D("netstat -ano"),
        D("netstat -an"),
        D("netstat -r"),
        D("arp -a"),
        D("route print"),
        D("hostname"),
        D("getmac /v"),
        D("whoami"),
        D("whoami /groups"),
        D("systeminfo"),
        D("tasklist"),
        D("taskkill /PID 1234 /F"),
        D("netsh wlan show profiles"),
        D("netsh interface ip show config"),
        D("netsh advfirewall show allprofiles"),
        D("nbtstat -n"),
        D("net user"),
        D("net localgroup administrators"),
        D("net use"),
        D("sfc /scannow"),
        D("chkdsk /f"),
        D("gpupdate /force"),
        D("gpresult /r"),
        D("shutdown /r /t 0"),
        D("dir /s"),
        D("cd .."),
        D("cls"),
        D("mkdir labs"),
        D("copy file.txt backup.txt"),
        D("del temp.txt"),
        D("type notes.txt"),
        D("echo %PATH%"),
        D("tree /f"),
        D("attrib +h secret.txt"),
        D(@"robocopy C:\src D:\dst /mir"),
        D("powercfg /batteryreport"),
        D("driverquery"),
        D("schtasks /query"),

        // PowerShell
        P("Get-NetIPAddress"),
        P("Get-NetIPConfiguration"),
        P("Get-NetAdapter"),
        P("Get-NetTCPConnection"),
        P("Get-NetRoute"),
        P("Get-NetNeighbor"),
        P("Test-NetConnection google.com -Port 443"),
        P("Test-Connection 8.8.8.8"),
        P("Resolve-DnsName google.com"),
        P("Clear-DnsClientCache"),
        P("Get-DnsClientServerAddress"),
        P("Get-NetFirewallProfile"),
        P("Get-Process"),
        P("Stop-Process -Name notepad"),
        P("Get-Service"),
        P("Get-Service | Where-Object Status -eq Running"),
        P("Restart-Service Spooler"),
        P("Get-ChildItem -Recurse"),
        P(@"Set-Location C:\Users"),
        P(@"Get-Content .\log.txt -Tail 20"),
        P("Get-Help Get-Process"),
        P("Get-Command *net*"),
        P("Get-LocalUser"),
        P("Get-LocalGroupMember Administrators"),
        P("Get-ComputerInfo"),
        P("Get-EventLog -LogName System -Newest 10"),
        P("Get-WinEvent -LogName Security -MaxEvents 5"),
        P("Get-ExecutionPolicy"),
        P("Set-ExecutionPolicy RemoteSigned"),
        P("Get-SmbShare"),
        P("Enter-PSSession -ComputerName SRV01"),
        P("Invoke-WebRequest https://example.com"),
        P("Get-Disk"),
        P("Get-Volume"),
        P("Get-HotFix"),
        P("New-Item -ItemType Directory labs"),
        P(@"Remove-Item .\temp.txt"),
        P(@"Copy-Item .\a.txt .\b.txt"),
        P("Select-String -Path *.log -Pattern error"),
    ];
}
