using Microsoft.AspNetCore.SignalR.Client;

namespace FreshPager.Toast.Eventing;

public class DelayRetry(Func<long, TimeSpan> delay): IRetryPolicy {

    public TimeSpan? NextRetryDelay(RetryContext retryContext) => delay(retryContext.PreviousRetryCount);

}