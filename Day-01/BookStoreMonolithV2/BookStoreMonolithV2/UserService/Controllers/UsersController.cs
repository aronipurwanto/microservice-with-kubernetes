using Microsoft.AspNetCore.Mvc;
using UserService.Services;
using SharedModels.Models;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> CreateUser(UserCreateDto userCreateDto)
    {
        try
        {
            var user = await _userService.CreateUserAsync(userCreateDto);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetUser(Guid id)
    {
        var user = await _userService.GetUserAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        return user;
    }

    [HttpGet("email/{email}")]
    public async Task<ActionResult<UserResponseDto>> GetUserByEmail(string email)
    {
        var user = await _userService.GetUserByEmailAsync(email);
        if (user == null)
        {
            return NotFound();
        }
        return user;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserResponseDto>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return users;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }
}