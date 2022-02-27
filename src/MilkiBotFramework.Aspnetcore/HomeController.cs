using Microsoft.AspNetCore.Mvc;

namespace MilkiBotFramework.Aspnetcore
{
    public class HomeController : ControllerBase
    {
        [NamespaceConstraint]
        public async Task<IActionResult> Index(string? content)
        {
            return Content(content ?? "");
        }
    }
}
