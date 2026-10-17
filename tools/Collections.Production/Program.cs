if (args.Length == 8 && args[0] == "certify-rehearsal")
    await ProductionTransfer.CertifyAsync(args[1], args[2], args[3..7], args[7]);
else if (args.Length == 4 && args[0] == "plan-live")
    await ProductionTransfer.PlanAsync(args[1], args[2], null, args[3]);
else if (args.Length == 8 && args[0] == "plan-rehearsal")
    await ProductionTransfer.PlanAsync(args[1], args[2], args[3..7], args[7]);
else if (args.Length == 8 && args[0] is "apply" or "rollback-check" or "resilience-check" or "verify-existing")
    await ProductionTransfer.RunAsync(args[0], args[1], args[2], args[3], args[4], args[5], args[6], args[7]);
else
    throw new ArgumentException("Kullanım: plan-live <bundle> <bundle SHA> <yeni çıktı> | plan-rehearsal <bundle> <bundle SHA> <capture> <review> <payments> <relations> <yeni çıktı> | apply/rollback-check/resilience-check/verify-existing <config> <plan> <plan SHA> <onay> <onay SHA> <yeni çıktı> <teyit>. Canlı yazma ayrı onaylıdır; eski prova aracının kilidi kaldırılmaz.");
