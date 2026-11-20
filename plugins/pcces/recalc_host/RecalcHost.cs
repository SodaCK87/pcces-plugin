// RecalcHost：不開 PCCES 畫面，直接叫 PCCES 4.3 自己的 DLL 做「重新總計」，給 pcx_mcp 的 pcces_recalc_db_budget 用。
// 呼叫鏈照 PccesMain.exe 工具列 Recalculate 追下來（frmBudget.DoMenuAction -> Th_ReCal_All(false) -> Do_ReCal_All
// -> DoNewCalculate），只留不碰畫面的那幾步。用 .NET 3.5 的 csc 編成 x86、CLR 2，跟 PccesMain.exe 同一套執行環境。
//
// 用法：RecalcHost.exe check|recalc <工程代碼> <PCCES 安裝資料夾>
//   check   只讀：跑完全部核對，退出碼跟 recalc 在同一個狀態下會拒跑的退出碼相同
//   recalc  核對都過了才寫：說明項單價歸零、發包權限同步、CalculateAll、清重新總計旗標與自主檢查
// 預設 AppDomain 只開一個子 AppDomain：ApplicationBase＝安裝資料夾、設定檔＝安裝資料夾的 PccesMain.exe.config，
// 所以連線字串與 appSettings 讀的是 PCCES 自己那份、組件照 PCCES 的探測路徑載入，宿主旁邊不必放 .config。
// 輸出：UTF-8，一行一筆「鍵<TAB>值<TAB>…」，鍵與值都不放中文說明，給使用者看的話由 Python 端組。
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Archnowledge.Common;
using Archnowledge.Pcces.CommonClass;
using Archnowledge.Pcces.DomainModule.Budget;
using Archnowledge.Pcces.DomainModule.BusinessLogical;
using Archnowledge.Pcces.DomainModule.General;

static class RecalcHost
{
    // 退出碼，Python 端 src\pcx_mcp\recalc.py 的 EXIT_* 跟這裡一一對應
    const int OK = 0, USAGE = 2, SETUP = 3, OLD_CALC = 4, CALC_FAILED = 5, EDITING = 6, VERSION = 7,
              SIGNATURE = 8, CRASH = 9, NOT_FOUND = 10, PCCES_RUNNING = 11, NOT_ALLOWED = 12;

    // 驗過的 PCCES。DLL 的組件版本一律是 1.0.0.0、分不出改版，所以重新總計會載入的每個 PCCES 檔都比 SHA256；
    // 清單是實測一次 recalc 後從 AppDomain 列出來、在安裝資料夾裡的組件（輸出的 loaded 行）。
    const string PccesVersion = "4.3.1000.220";
    static readonly string[] Pinned = {
        "PccesMain.exe", "bbef3e062dd4a8819883f6c12a0f0f9cb8669c912e9a001c559daca4a3387e6c",
        "Archnowledge.Pcces.DomainModule.BusinessLogical.dll", "730714f8f4f4468e36bfe0309312f189676c074be7902ee488ae4e609f3df31e",
        "CommomClass.dll", "20d9e1ddbad57d6b550ee25c2b530be00e1ee3d479c0ca8a1c587dfe9438bd72",
        "Archnowledge.Common.dll", "ea0ff25629a99793d1f3067c412c57e88f85ece2fe55f87161ff03a35fce2e2a",
        "Archnowledge.Pcces.DomainModule.General.dll", "9f3ac72b2f61029b822d350e13435c226929598c8d88a56e95ffaad36040f311",
        "Archnowledge.Pcces.DomainModule.Budget.dll", "e7d3511139818cef216ac05df10a8ee2535dc7d82e3f32e16bf51c5cabf8dc58",
        "Archnowledge.Pcces.DomainModule.LogicalBase.dll", "52617d02f324d17c1d8970b88ffeea0b190ba425456527c84e3214bc3b0421ed",
        "Archnowledge.WebCore.SystemBase.dll", "06c9e24fd6ba24e3ba850a00d14ff4439f1bd13bfa0fc24318d79fca21e8af7a",
        "Archnowledge.Pcces.DatabaseAccess.dll", "184be8b50c6e558958fb7ef8d1b1df4768c24325dcbe4a2d92117515a1812a0c",
        "Archnowledge.Pcces.DomainModule.Sub.dll", "27fadbe29ffab0e36344589d1c80aec1d10d7c37d34aea6b1fac5fe9a15eb448",
        "Archnowledge.Pcces.DomainModule.MrsBase.dll", "3bb0bcf3791ea52e17f09f33bb7cb54e86868f7413609ab127edf8b47d7dd65d",
        "Archnowledge.Pcces.DomainModule.Bid.dll", "889199128b5b9ea2cc06dd23f49bc2cdf2b5ed0a6a66d94f97a3a3bb280f2f6f",
        "stdclass.dll", "a6ad428f40050857175a5abf433bf76d60372d1f49e286b5273f846d414dbe37",
    };

    // 單一編輯鎖定（SysConfig 的 SingleEditLockMode）預設關著，關著時 PCCES 開預算書不會寫 BudProjEditUser，
    // 資料庫分不出 PCCES 開著哪一本，所以這台有 PccesMain 在跑就拒跑。探針的變體才把它改成 false。
    const bool CheckPccesProcess = true;

    static TextWriter W;

    static int Main(string[] args)
    {
        StreamWriter sw = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        sw.AutoFlush = true;
        W = sw;
        try
        {
            return AppDomain.CurrentDomain.IsDefaultAppDomain() ? Launch(args) : Run(args);
        }
        catch (Exception ex) { Out("exception", OneLine(ex.ToString())); return CRASH; }
    }

    static void Out(string key, params object[] values)
    {
        StringBuilder b = new StringBuilder(key);
        foreach (object v in values) b.Append('\t').Append(OneLine(Convert.ToString(v, CultureInfo.InvariantCulture)));
        W.WriteLine(b.ToString());
    }

    static string OneLine(string s) { return (s ?? "").Replace("\r", "").Replace("\n", " | ").Replace("\t", " "); }

    static int Launch(string[] args)
    {
        if (args.Length != 3 || (args[0] != "check" && args[0] != "recalc"))
        {
            Out("usage", "RecalcHost.exe check|recalc <projectCode> <installDir>");
            return USAGE;
        }
        string dir = Path.GetFullPath(args[2]);
        string config = Path.Combine(dir, "PccesMain.exe.config");
        if (!File.Exists(Path.Combine(dir, "PccesMain.exe")) || !File.Exists(config))
        {
            Out("setup", "missing_install", dir);
            return SETUP;
        }
        AppDomainSetup s = new AppDomainSetup();
        s.ApplicationBase = dir;
        s.ConfigurationFile = config;
        s.ApplicationName = "RecalcHost";
        AppDomain d = AppDomain.CreateDomain("pcces", null, s);
        try { return d.ExecuteAssembly(Assembly.GetExecutingAssembly().Location, null, args); }
        finally { try { AppDomain.Unload(d); } catch (Exception) { } }
    }

    // 子 AppDomain 的進入點。這一層不准直接用 PCCES 的型別：型別在 NoInlining 的方法裡才 JIT，載不起來也收得到
    static int Run(string[] args)
    {
        string mode = args[0], code = args[1].Trim();
        string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        Out("runtime", Environment.Version, IntPtr.Size == 4 ? "x86" : "x64");
        Out("config", AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
        Stopwatch total = Stopwatch.StartNew();

        // 1. 版本：檔案內容先比，對不上就不載入 PCCES 的型別
        if (!CheckFiles(dir)) return VERSION;
        // 2. 方法簽章
        try { if (!CheckSignatures(dir)) return SIGNATURE; }
        catch (Exception ex) { Out("sig_error", ex.GetType().Name, ex.Message); return SIGNATURE; }

        ConnectionStringSettings css = ConfigurationManager.ConnectionStrings["Pcces"];
        if (css == null || string.IsNullOrEmpty(css.ConnectionString)) { Out("setup", "no_connection_string"); return SETUP; }
        string cs = css.ConnectionString;
        SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder(cs);
        Out("database", csb.DataSource, csb.InitialCatalog);

        // 3. 工程、白名單、算法、編輯鎖定、PCCES 是否在跑
        string pc = null, name = "";
        object amount = null;
        List<string[]> editors;
        using (SqlConnection cn = new SqlConnection(cs))
        {
            cn.Open();
            SqlCommand c = new SqlCommand("SELECT RTRIM(projectCode), ISNULL(RTRIM(projectNameC), ''), projamt FROM budProject WHERE projectCode = @pc", cn);
            c.Parameters.AddWithValue("@pc", code);
            using (SqlDataReader r = c.ExecuteReader())
                if (r.Read()) { pc = r.GetString(0); name = r.GetString(1); amount = r.GetValue(2); }
            editors = pc == null ? new List<string[]>() : Editors(cn, pc);
        }
        if (pc == null) { Out("not_found", code); return NOT_FOUND; }
        Out("project", pc, name);
        Out("projamt_before", amount);
        // 測試與探針設 PCX_RECALC_ALLOW 把可寫的工程限死，產品不設
        string allow = Environment.GetEnvironmentVariable("PCX_RECALC_ALLOW");
        if (!string.IsNullOrEmpty(allow) && !InList(allow, pc)) { Out("not_allowed", pc, allow); return NOT_ALLOWED; }

        bool enableNew = EnableNew(pc);
        Out("enableNewCalculateCost", enableNew);
        Out("single_edit_lock_mode", LockMode());
        // 舊算法（ItemA.ReCalcCost2 那一路）沒接，不用新算法冒充
        if (!enableNew) return OLD_CALC;
        foreach (string[] e in editors) Out("editor", e[0], e[1], e[2]);
        if (editors.Count > 0) return EDITING;
        int[] pids = PccesPids();
        foreach (int p in pids) Out("pcces_process", p);
        if (CheckPccesProcess && pids.Length > 0) return PCCES_RUNNING;
        Out("checks", "ok", total.ElapsedMilliseconds);
        if (mode == "check") return OK;

        int rc = Recalc(cs, pc);
        using (SqlConnection cn = new SqlConnection(cs))
        {
            cn.Open();
            SqlCommand c = new SqlCommand("SELECT projamt FROM budProject WHERE projectCode = @pc", cn);
            c.Parameters.AddWithValue("@pc", pc);
            Out("projamt_after", c.ExecuteScalar());
            // 跑的期間有人在 PCCES 開了這本：照實回報，畫面上可能是舊值
            foreach (string[] e in Editors(cn, pc)) Out("editor_after", e[0], e[1], e[2]);
        }
        foreach (int p in PccesPids()) Out("pcces_process_after", p);
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            string loc = "";
            try { loc = a.Location; } catch (NotSupportedException) { }
            if (loc.Length == 0 || !string.Equals(Path.GetDirectoryName(loc), dir, StringComparison.OrdinalIgnoreCase)) continue;
            string file = Path.GetFileName(loc);
            Out("loaded", file);
            // 用到了沒比過雜湊的 PCCES 組件：Pinned 清單不完整，版本核對沒涵蓋到它
            if (!IsPinned(file)) Out("unpinned", file);
        }
        Out("ms_total", total.ElapsedMilliseconds);
        return rc;
    }

    static bool IsPinned(string file)
    {
        for (int i = 0; i < Pinned.Length; i += 2)
            if (string.Equals(Pinned[i], file, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static bool InList(string list, string pc)
    {
        foreach (string s in list.Split(','))
            if (string.Equals(s.Trim(), pc, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static List<string[]> Editors(SqlConnection cn, string pc)
    {
        List<string[]> list = new List<string[]>();
        SqlCommand c = new SqlCommand("SELECT RTRIM(UserID), ISNULL(UserName, ''), CONVERT(VARCHAR(19), EditStartDate, 120) FROM BudProjEditUser WHERE projectCode = @pc", cn);
        c.Parameters.AddWithValue("@pc", pc);
        using (SqlDataReader r = c.ExecuteReader())
            while (r.Read()) list.Add(new string[] { r.GetString(0), r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2) });
        return list;
    }

    static int[] PccesPids()
    {
        Process[] ps = Process.GetProcessesByName("PccesMain");
        int[] ids = new int[ps.Length];
        for (int i = 0; i < ps.Length; i++) ids[i] = ps[i].Id;
        return ids;
    }

    static string Sha256(string path)
    {
        using (SHA256Managed h = new SHA256Managed())
        using (FileStream f = File.OpenRead(path))
            return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
    }

    static bool CheckFiles(string dir)
    {
        bool ok = true;
        string main = Path.Combine(dir, "PccesMain.exe");
        string ver = AssemblyName.GetAssemblyName(main).Version.ToString();
        bool vok = ver == PccesVersion;
        Out("pcces_version", ver, vok ? "ok" : "bad", PccesVersion);
        ok &= vok;
        for (int i = 0; i < Pinned.Length; i += 2)
        {
            string p = Path.Combine(dir, Pinned[i]);
            if (!File.Exists(p)) { Out("file", Pinned[i], "missing", Pinned[i + 1]); ok = false; continue; }
            string got = Sha256(p);
            bool hit = got == Pinned[i + 1];
            Out("file", Pinned[i], hit ? "ok" : "bad", Pinned[i + 1], got);
            ok &= hit;
        }
        return ok;
    }

    static bool Sig(string what, bool ok)
    {
        Out("sig", what, ok ? "ok" : "missing");
        return ok;
    }

    static bool Returns(MethodInfo m, Type t) { return m != null && m.ReturnType == t; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool CheckSignatures(string dir)
    {
        Type ic = typeof(ItemCalculate), fa = typeof(PccesFormAction), er = typeof(ExecResult);
        Type h1 = typeof(CalculateEventHandler), h2 = typeof(CalculateEventHandlerInitMaxProgressValue);
        Type[] str1 = { typeof(string) }, str2 = { typeof(string), typeof(string) };
        bool ok = true;
        ok &= Sig("ItemCalculate(PccesFormAction,String,Int32)", ic.GetConstructor(new Type[] { fa, typeof(string), typeof(int) }) != null);
        ok &= Sig("PccesFormAction.BUD=2", Enum.IsDefined(fa, "BUD") && Convert.ToInt32(Enum.Parse(fa, "BUD")) == 2);
        ok &= Sig("ItemCalculate.CalculateAll(Boolean,Boolean,CalculateEventHandler,CalculateEventHandlerInitMaxProgressValue):ExecResult",
                  Returns(ic.GetMethod("CalculateAll", new Type[] { typeof(bool), typeof(bool), h1, h2 }), er));
        ok &= Sig("ExecResult.ReturnCode:Int32,Message:String",
                  er.GetProperty("ReturnCode") != null && er.GetProperty("ReturnCode").PropertyType == typeof(int)
                  && er.GetProperty("Message") != null && er.GetProperty("Message").PropertyType == typeof(string));
        ok &= Sig("PubProject.GetPubProjectEnableNewCalculateCost(String):Boolean",
                  Returns(typeof(PubProject).GetMethod("GetPubProjectEnableNewCalculateCost", str1), typeof(bool)));
        ok &= Sig("PwrSet.Synchronize(String):ExecResult", Returns(typeof(PwrSet).GetMethod("Synchronize", str1), er));
        ok &= Sig("BudProject.UpdateProjectIsReCal(String,String)", typeof(BudProject).GetMethod("UpdateProjectIsReCal", str2) != null);
        ok &= Sig("BudProject.UpdateSelfExam(String,String)", typeof(BudProject).GetMethod("UpdateSelfExam", str2) != null);
        PropertyInfo lm = typeof(SysConfig).GetProperty("SysSingleEditLockMode", BindingFlags.Public | BindingFlags.Static);
        ok &= Sig("SysConfig.SysSingleEditLockMode:Boolean", lm != null && lm.PropertyType == typeof(bool));
        // 載入的要是安裝資料夾裡、上面比過雜湊的那一份，不是別處的同名組件
        foreach (Type t in new Type[] { ic, fa, er, typeof(PubProject), typeof(BudProject) })
            ok &= Sig("loaded_from_install_dir:" + t.Assembly.GetName().Name,
                      string.Equals(Path.GetDirectoryName(t.Assembly.Location), dir, StringComparison.OrdinalIgnoreCase));
        return ok;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool EnableNew(string pc) { return new PubProject().GetPubProjectEnableNewCalculateCost(pc); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool LockMode() { return SysConfig.SysSingleEditLockMode; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Recalc(string cs, string pc)
    {
        Out("stage", "write_begin");
        // 1. SetMemoItemCostToZero（PccesMain 內的兩句 SQL）：costKind '#' 的說明項單價歸零。
        //    原句是「Update budItemA Set budItemA.cost = 0 From budItemA A Join …」，這裡改成以別名為目標的同義寫法，範圍一樣限本工程
        Stopwatch sw = Stopwatch.StartNew();
        using (SqlConnection cn = new SqlConnection(cs))
        {
            cn.Open();
            SqlCommand c1 = new SqlCommand("UPDATE A SET A.cost = 0 FROM budItemA A JOIN budProjMrsA B ON A.PccesCode = B.pccesCode AND A.projectCode = B.projectCode WHERE A.projectCode = @pc AND B.costKind = '#' AND A.cost <> 0", cn);
            c1.Parameters.AddWithValue("@pc", pc);
            Out("memo_itemA_rows", c1.ExecuteNonQuery());
            SqlCommand c2 = new SqlCommand("UPDATE budProjMrsA SET cost = 0 WHERE projectCode = @pc AND costKind = '#' AND cost <> 0", cn);
            c2.Parameters.AddWithValue("@pc", pc);
            Out("memo_mrsA_rows", c2.ExecuteNonQuery());
        }
        Out("ms_memo", sw.ElapsedMilliseconds);

        // 2. 發包權限同步（usp_pcces_SyncPwsSet，只動本工程的 PwrSet 欄）
        sw = Stopwatch.StartNew();
        ExecResult pr = new PwrSet().Synchronize(pc);
        Out("pwrset", pr.ReturnCode, pr.Message);
        Out("ms_pwrset", sw.ElapsedMilliseconds);

        // 3. DoNewCalculate 的核心：new ItemCalculate(BUD, pc, 0).CalculateAll(true, true, …)
        int msgCount = 0, maxProgress = 0;
        string lastMsg = "";
        CalculateEventHandler onMsg = delegate(string m) { msgCount++; lastMsg = m; };
        CalculateEventHandlerInitMaxProgressValue onMax = delegate(int m) { maxProgress = m; };
        Out("stage", "calculate");
        sw = Stopwatch.StartNew();
        ExecResult r = new ItemCalculate(PccesFormAction.BUD, pc, 0).CalculateAll(true, true, onMsg, onMax);
        Out("ms_calculate", sw.ElapsedMilliseconds);
        Out("calc", r.ReturnCode, r.Message);
        Out("progress", msgCount, maxProgress, lastMsg);
        if (r.ReturnCode != 0) { Out("stage", "calc_failed"); return CALC_FAILED; }

        // 4. 成功後畫面會做的兩筆狀態寫入：IsReCal='N'、自主檢查清空
        sw = Stopwatch.StartNew();
        new BudProject().UpdateProjectIsReCal(pc, "N");
        new BudProject().UpdateSelfExam(pc, "000000");
        Out("ms_flags", sw.ElapsedMilliseconds);
        Out("stage", "done");
        return OK;
    }
}
