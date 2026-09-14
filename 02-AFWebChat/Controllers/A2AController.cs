using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

/// <summary>
/// API de descubrimiento del protocolo A2A: qué agentes de AF-WebChat están publicados como
/// servidores A2A y qué agentes A2A remotos están conectados. Útil para demos e integraciones.
/// </summary>
[ApiController]
[Route("api/a2a")]
public class A2AController : ControllerBase
{
    private readonly A2ADirectory _directory;

    public A2AController(A2ADirectory directory)
    {
        _directory = directory;
    }

    [HttpGet]
    public ActionResult<A2AInfo> Get() => Ok(_directory.ToInfo());
}
