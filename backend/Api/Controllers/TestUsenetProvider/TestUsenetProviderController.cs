using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Clients;

namespace NzbWebDAV.Api.Controllers.TestUsenetProvider;

[ApiController]
[Route("api/usenet-providers/{providerId}/test")]
public class TestUsenetProviderController(UsenetProviderManager providerManager) : BaseApiController
{
    protected override async Task<IActionResult> HandleRequest()
    {
        var providerId = HttpContext.Request.RouteValues["providerId"]?.ToString();
        if (string.IsNullOrEmpty(providerId))
        {
            return BadRequest(new TestUsenetProviderResponse 
            { 
                Status = false, 
                Connected = false, 
                ErrorMessage = "Provider ID is required" 
            });
        }

        try
        {
            var result = await providerManager.TestConnectionAsync(providerId, HttpContext.RequestAborted);
            return Ok(new TestUsenetProviderResponse 
            { 
                Status = true, 
                Connected = result 
            });
        }
        catch (Exception ex)
        {
            return Ok(new TestUsenetProviderResponse 
            { 
                Status = true, 
                Connected = false, 
                ErrorMessage = ex.Message 
            });
        }
    }
}