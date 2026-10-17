try
{
    if (args.Length == 9 && args[0] is "plan-rehearsal" or "plan-live")
        await FileAdapter.PlanAsync(args[0], args[1], args[2], args[3], args[4], args[5], long.Parse(args[6]), args[7], args[8]);
    else if (args.Length == 6 && args[0] == "rehearse")
        await FileRehearsal.RunAsync(args[1], args[2], args[3], args[4], args[5]);
    else if (args.Length == 8 && args[0] == "apply-live")
        await FileAdapter.ApplyLiveAsync(args[1], args[2], args[3], args[4], args[5], args[6], args[7]);
    else throw new InvalidOperationException("Kullanım: plan-rehearsal/plan-live <config> <bundle> <bundle SHA> <Contract kökü> <Aktivite.rar> <batch ID> <finansal acceptance.json> <yeni çıktı> | rehearse <config> <plan.json> <plan SHA> <yeni çıktı> <REHEARSAL_ONLY> | apply-live <config> <plan.json> <plan SHA> <onay.json> <onay SHA> <yeni çıktı> <APPLY_LIVE_FILES>.");
}
catch (Exception ex)
{
    // Provider/SQL exceptions can include credentials, paths and customer information.
    Console.Error.WriteLine("Dosya adaptörü güvenli durdu. Hata sınıfı: " + ex.GetType().Name);
    if (ex is FileGateException) Console.Error.WriteLine(ex.Message);
    if (ex is Microsoft.Data.SqlClient.SqlException sql) Console.Error.WriteLine($"SQL hata kodu: {sql.Number}; State: {sql.State}; Class: {sql.Class}.");
    Environment.ExitCode = 1;
}
