if (args.Length == 7 && args[0] == "plan")
{
    await RehearsalTransfer.PlanAsync(args[1], args[2], args[3], args[4], args[5], args[6]);
    return;
}
if (args.Length == 6 && args[0] is "apply" or "rollback-check" or "resilience-check")
{
    await RehearsalTransfer.RunAsync(args[0], args[1], args[2], args[3], args[4], args[5]);
    return;
}
throw new ArgumentException("Kullanım: plan <config> <capture> <review> <payments> <relations> <yeni çıktı> | apply/rollback-check/resilience-check <config> <plan> <plan SHA> <yeni çıktı> <REHEARSAL_ONLY>. Yalnız AssistFlow_Performance_Restore; production hedefi yoktur.");
