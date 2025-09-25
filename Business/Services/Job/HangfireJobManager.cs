//using Business.Interfaces.Job;
//using Business.UnitOfWork;
//using Core.Utilities.Constants;
//using Hangfire.Console;
//using Hangfire.Server;
//using Microsoft.Extensions.Logging;


//namespace Business.Services.Job
//{
//    public class HangfireJobManager : IHangfireJobService
//    {

//        private readonly ILogger<HangfireJobManager> _logger;

//        private readonly IUnitOfWork _uow;

//        public HangfireJobManager(ILogger<HangfireJobManager> logger, IUnitOfWork uow)
//        {
//            _logger = logger;
//            _uow = uow;
//        }
//        public async Task Work(PerformContext context)
//        {
//            var progress = context.WriteProgressBar();
//            try
//            {

//                progress.SetValue(100);
//                context.WriteLine(Messages.OperationComplate);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogDebug(ex, Messages.AnErrorOccurred, ex.Message);
//                context.WriteLine(ex.Message);
//                progress.SetValue(100);
//            }
//        }
//    }
//}
