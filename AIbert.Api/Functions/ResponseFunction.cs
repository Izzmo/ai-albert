using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions
{
    public class ResponseFunction
    {
        private readonly ILogger _logger;

        public ResponseFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ResponseFunction>();
        }

        [Function("ResponseFunction")]
        public void Run([TimerTrigger("0 */5 * * * *")] Microsoft.Azure.WebJobs.TimerInfo myTimer)
        {
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
