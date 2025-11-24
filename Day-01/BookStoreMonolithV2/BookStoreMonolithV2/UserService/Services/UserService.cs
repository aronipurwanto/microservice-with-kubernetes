using SharedModels.Models;
using System.Collections.Concurrent;

namespace UserService.Services;

public interface IUserService
{
    Task<UserResponseDto> CreateUserAsync(UserCreateDto userCreateDto);
    Task<UserResponseDto> GetUserAsync(Guid id);
    Task<UserResponseDto> GetUserByEmailAsync(string email);
    Task<List<UserResponseDto>> GetAllUsersAsync();
    Task<bool> DeleteUserAsync(Guid id);
}

public class UserService : IUserService
{
    private static readonly ConcurrentDictionary<Guid, User> _users = new();
    private static readonly ConcurrentDictionary<string, Guid> _emailIndex = new();

    static UserService()
    {
        // Seed dummy data
        var dummyUsers = new[]
        {
            new User { Id = Guid.NewGuid(), Email = "john.doe@example.com", FirstName = "John", LastName = "Doe", CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Email = "jane.smith@example.com", FirstName = "Jane", LastName = "Smith", CreatedAt = DateTime.UtcNow },
            new User { Id = Guid.NewGuid(), Email = "bob.johnson@example.com", FirstName = "Bob", LastName = "Johnson", CreatedAt = DateTime.UtcNow }
        };

        foreach (var user in dummyUsers)
        {
            _users[user.Id] = user;
            _emailIndex[user.Email.ToLower()] = user.Id;
        }
    }

    public Task<UserResponseDto> CreateUserAsync(UserCreateDto userCreateDto)
    {
        if (_emailIndex.ContainsKey(userCreateDto.Email.ToLower()))
        {
            throw new ArgumentException("User with this email already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = userCreateDto.Email,
            FirstName = userCreateDto.FirstName,
            LastName = userCreateDto.LastName,
            CreatedAt = DateTime.UtcNow
        };

        _users[user.Id] = user;
        _emailIndex[user.Email.ToLower()] = user.Id;

        return Task.FromResult(MapToUserResponseDto(user));
    }

    public Task<UserResponseDto> GetUserAsync(Guid id)
    {
        if (_users.TryGetValue(id, out var user))
        {
            return Task.FromResult(MapToUserResponseDto(user));
        }
        return Task.FromResult<UserResponseDto>(null);
    }

    public Task<UserResponseDto> GetUserByEmailAsync(string email)
    {
        if (_emailIndex.TryGetValue(email.ToLower(), out var userId) && 
            _users.TryGetValue(userId, out var user))
        {
            return Task.FromResult(MapToUserResponseDto(user));
        }
        return Task.FromResult<UserResponseDto>(null);
    }

    public Task<List<UserResponseDto>> GetAllUsersAsync()
    {
        var users = _users.Values
            .Select(MapToUserResponseDto)
            .ToList();
        return Task.FromResult(users);
    }

    public Task<bool> DeleteUserAsync(Guid id)
    {
        if (_users.TryRemove(id, out var user))
        {
            _emailIndex.TryRemove(user.Email.ToLower(), out _);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private static UserResponseDto MapToUserResponseDto(User user)
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