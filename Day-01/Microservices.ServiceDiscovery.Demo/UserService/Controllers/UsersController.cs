using Microsoft.AspNetCore.Mvc;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private static readonly List<User> _users = new()
    {
        new User(1, "john.doe@example.com", "John", "Doe", "active", DateTime.UtcNow.AddDays(-30)),
        new User(2, "jane.smith@example.com", "Jane", "Smith", "active", DateTime.UtcNow.AddDays(-15)),
        new User(3, "bob.johnson@example.com", "Bob", "Johnson", "inactive", DateTime.UtcNow.AddDays(-60))
    };

    private readonly ILogger<UsersController> _logger;

    public UsersController(ILogger<UsersController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<User>> GetUsers()
    {
        _logger.LogInformation("Retrieving all users. Total: {UserCount}", _users.Count);
        return Ok(new
        {
            totalUsers = _users.Count,
            users = _users
        });
    }

    [HttpGet("{id}")]
    public ActionResult<User> GetUser(int id)
    {
        _logger.LogInformation("Retrieving user with ID: {UserId}", id);
        
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found", id);
            return NotFound(new { error = $"User with ID {id} not found" });
        }
        
        return Ok(user);
    }

    [HttpGet("email/{email}")]
    public ActionResult<User> GetUserByEmail(string email)
    {
        _logger.LogInformation("Retrieving user with email: {Email}", email);
        
        var user = _users.FirstOrDefault(u => 
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        
        if (user == null)
        {
            _logger.LogWarning("User with email {Email} not found", email);
            return NotFound(new { error = $"User with email {email} not found" });
        }
        
        return Ok(user);
    }

    [HttpPost]
    public ActionResult<User> CreateUser(CreateUserRequest request)
    {
        _logger.LogInformation("Creating new user with email: {Email}", request.Email);
        
        // Validate email uniqueness
        if (_users.Any(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("User with email {Email} already exists", request.Email);
            return Conflict(new { error = $"User with email {request.Email} already exists" });
        }

        var newUser = new User(
            _users.Count + 1,
            request.Email,
            request.FirstName,
            request.LastName,
            "active",
            DateTime.UtcNow
        );

        _users.Add(newUser);
        
        _logger.LogInformation("User created with ID: {UserId}", newUser.Id);
        
        return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, newUser);
    }

    [HttpPut("{id}")]
    public ActionResult<User> UpdateUser(int id, UpdateUserRequest request)
    {
        _logger.LogInformation("Updating user with ID: {UserId}", id);
        
        var existingUser = _users.FirstOrDefault(u => u.Id == id);
        if (existingUser == null)
        {
            _logger.LogWarning("User with ID {UserId} not found for update", id);
            return NotFound(new { error = $"User with ID {id} not found" });
        }

        // In a real application, you would update the entity properly
        var updatedUser = existingUser with 
        { 
            FirstName = request.FirstName ?? existingUser.FirstName,
            LastName = request.LastName ?? existingUser.LastName,
            Status = request.Status ?? existingUser.Status
        };

        _users.Remove(existingUser);
        _users.Add(updatedUser);
        
        _logger.LogInformation("User with ID {UserId} updated successfully", id);
        
        return Ok(updatedUser);
    }

    [HttpGet("status/{status}")]
    public ActionResult<IEnumerable<User>> GetUsersByStatus(string status)
    {
        _logger.LogInformation("Retrieving users with status: {Status}", status);
        
        var users = _users.Where(u => 
            string.Equals(u.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return Ok(new
        {
            status = status,
            totalUsers = users.Count,
            users = users
        });
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteUser(int id)
    {
        _logger.LogInformation("Deleting user with ID: {UserId}", id);
        
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found for deletion", id);
            return NotFound(new { error = $"User with ID {id} not found" });
        }

        _users.Remove(user);
        
        _logger.LogInformation("User with ID {UserId} deleted successfully", id);
        
        return NoContent();
    }
}

// User model and request DTOs
public record User(int Id, string Email, string FirstName, string LastName, string Status, DateTime CreatedAt);
public record CreateUserRequest(string Email, string FirstName, string LastName);
public record UpdateUserRequest(string? FirstName, string? LastName, string? Status);