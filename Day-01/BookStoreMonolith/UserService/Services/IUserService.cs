using Microsoft.EntityFrameworkCore;
using SharedModels.Models;
using UserService.Models;

namespace UserService.Services;

public interface IUserService
{
    Task<UserResponseDto?> GetUserAsync(Guid id);
    Task<UserResponseDto> CreateUserAsync(UserCreateDto userDto);
    Task<bool> UpdateUserAsync(Guid id, UserCreateDto userDto);
    Task<bool> DeleteUserAsync(Guid id);
    Task<List<UserResponseDto>> GetAllUsersAsync();
}

public class UserService : IUserService
{
    private readonly UserContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(UserContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserResponseDto?> GetUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        return user == null ? null : MapToDto(user);
    }

    public async Task<UserResponseDto> CreateUserAsync(UserCreateDto userDto)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = userDto.Email,
            FirstName = userDto.FirstName,
            LastName = userDto.LastName,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created user with ID: {UserId}", user.Id);
        return MapToDto(user);
    }

    public async Task<bool> UpdateUserAsync(Guid id, UserCreateDto userDto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return false;

        user.Email = userDto.Email;
        user.FirstName = userDto.FirstName;
        user.LastName = userDto.LastName;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<UserResponseDto>> GetAllUsersAsync()
    {
        return await _context.Users
            .Select(u => MapToDto(u))
            .ToListAsync();
    }

    private static UserResponseDto MapToDto(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt
        };
    }
}