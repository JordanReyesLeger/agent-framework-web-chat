using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly SessionService _sessionService;

    public SessionController(SessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public ActionResult<List<SessionInfo>> GetAll()
    {
        return Ok(_sessionService.GetAllSessions());
    }

    [HttpGet("{id}")]
    public ActionResult<SessionInfo> Get(string id)
    {
        var session = _sessionService.GetSession(id);
        return session is not null ? Ok(session) : NotFound();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        return _sessionService.DeleteSession(id) ? Ok() : NotFound();
    }
}
