namespace FreshPager.API.Toast;

public class ToastResource: WebResource {

    public void map(WebApplication webapp) => webapp.MapHub<ToastHub>("/pagerduty/toasts");

}